using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace Pwneu.Api.Features.Analysis;

public static class GetLeaderboardsPerCategory
{
    public record Query() : IRequest<Result<byte[]>>;

    public record Response(
        string Id,
        string? UserName,
        string? Email,
        string Fullname,
        int Position,
        int Points,
        DateTime LatestSolve
    );

    internal sealed class Handler(AppDbContext context) : IRequestHandler<Query, Result<byte[]>>
    {
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
        {
            WriteIndented = true,
        };

        public async Task<Result<byte[]>> Handle(Query request, CancellationToken cancellationToken)
        {
            var categories = await context
                .Categories.Select(c => new { c.Id, c.Name })
                .ToListAsync(cancellationToken);

            await using var memoryStream = new MemoryStream();
            using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                foreach (var category in categories)
                {
                    var userStats = await context
                        .Users.Where(u => u.IsVisibleOnLeaderboards)
                        .Select(u => new
                        {
                            u.Id,
                            u.UserName,
                            u.Email,
                            u.FullName,
                            Points = u
                                .Solves.Where(s =>
                                    s.Challenge.Points > 0 && s.Challenge.CategoryId == category.Id
                                )
                                .Sum(s => s.Challenge.Points),
                            Deductions = u
                                .HintUsages.Where(hu => hu.Hint.Challenge.CategoryId == category.Id)
                                .Sum(hu => hu.Hint.Deduction),
                            LatestSolve = u
                                .Solves.Where(s =>
                                    s.Challenge.Points > 0 && s.Challenge.CategoryId == category.Id
                                )
                                .OrderByDescending(s => s.SolvedAt)
                                .Select(s => (DateTime?)s.SolvedAt)
                                .FirstOrDefault(),
                        })
                        .ToListAsync(cancellationToken);

                    var userRanks = userStats
                        .Select(u =>
                        {
                            var finalPoints = u.Points - u.Deductions;
                            return new
                            {
                                u.Id,
                                u.UserName,
                                u.Email,
                                u.FullName,
                                FinalPoints = finalPoints,
                                LatestSolve = u.LatestSolve ?? DateTime.MinValue,
                            };
                        })
                        .Where(u => u.FinalPoints > 0)
                        .OrderByDescending(u => u.FinalPoints)
                        .ThenBy(u => u.LatestSolve)
                        .Select(
                            (u, index) =>
                                new Response(
                                    u.Id,
                                    u.UserName,
                                    u.Email,
                                    u.FullName,
                                    index + 1,
                                    u.FinalPoints,
                                    u.LatestSolve
                                )
                        )
                        .ToList();

                    var csv = new StringBuilder();
                    csv.AppendLine("Id,UserName,Email,FullName,Position,Points,LatestSolve");
                    foreach (var user in userRanks)
                    {
                        csv.AppendLine(
                            $"{user.Id},{Escape(user.UserName)},{Escape(user.Email)},{Escape(user.Fullname)},{user.Position},{user.Points},{user.LatestSolve:O}"
                        );
                    }

                    var csvEntry = zipArchive.CreateEntry(
                        $"{SanitizeFileName(category.Name)}.csv",
                        CompressionLevel.Optimal
                    );
                    await using (var csvStream = csvEntry.Open())
                    {
                        var buffer = Encoding.UTF8.GetBytes(csv.ToString());
                        await csvStream.WriteAsync(buffer, cancellationToken);
                    }

                    var json = JsonSerializer.Serialize(userRanks, _jsonSerializerOptions);

                    var jsonEntry = zipArchive.CreateEntry(
                        $"{SanitizeFileName(category.Name)}.json",
                        CompressionLevel.Optimal
                    );
                    await using (var jsonStream = jsonEntry.Open())
                    {
                        var buffer = Encoding.UTF8.GetBytes(json);
                        await jsonStream.WriteAsync(buffer, cancellationToken);
                    }

                    var document = Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4);
                            page.Margin(2, QuestPDF.Infrastructure.Unit.Centimetre);

                            page.Content()
                                .Column(column =>
                                {
                                    column
                                        .Item()
                                        .Text($"PWNEU {category.Name} Leaderboards")
                                        .FontSize(20)
                                        .Bold()
                                        .AlignCenter();

                                    column
                                        .Item()
                                        .Text(
                                            $"Generated on: {DateTime.UtcNow:MMMM dd, yyyy HH:mm:ss 'UTC'}"
                                        )
                                        .FontSize(10)
                                        .Italic()
                                        .AlignCenter();

                                    column
                                        .Item()
                                        .Height(1, QuestPDF.Infrastructure.Unit.Centimetre);

                                    column
                                        .Item()
                                        .Table(table =>
                                        {
                                            table.ColumnsDefinition(columns =>
                                            {
                                                columns.ConstantColumn(60);
                                                columns.RelativeColumn();
                                                columns.ConstantColumn(70);
                                            });

                                            table.Header(header =>
                                            {
                                                header
                                                    .Cell()
                                                    .Element(CellStyle)
                                                    .Text("Position")
                                                    .Bold();
                                                header
                                                    .Cell()
                                                    .Element(CellStyle)
                                                    .Text("Username")
                                                    .Bold();
                                                header
                                                    .Cell()
                                                    .Element(CellStyle)
                                                    .Text("Points")
                                                    .Bold();
                                            });

                                            var rowIndex = 0;
                                            foreach (var user in userRanks)
                                            {
                                                var bg =
                                                    rowIndex++ % 2 == 0
                                                        ? Colors.White
                                                        : Colors.Grey.Lighten5;

                                                table
                                                    .Cell()
                                                    .Element(CellStyle)
                                                    .Background(bg)
                                                    .Text(user.Position.ToString());
                                                table
                                                    .Cell()
                                                    .Element(CellStyle)
                                                    .Background(bg)
                                                    .Text(user.UserName ?? "N/A");
                                                table
                                                    .Cell()
                                                    .Element(CellStyle)
                                                    .Background(bg)
                                                    .Text(user.Points.ToString());
                                            }
                                        });
                                });

                            page.Footer()
                                .AlignCenter()
                                .Text(txt =>
                                {
                                    txt.Span("Page ");
                                    txt.CurrentPageNumber();
                                    txt.Span(" of ");
                                    txt.TotalPages();
                                });
                        });
                    });

                    var entry = zipArchive.CreateEntry(
                        $"PWNEU {SanitizeFileName(category.Name)} Leaderboards.pdf",
                        CompressionLevel.Optimal
                    );
                    await using var pdfStream = entry.Open();
                    document.GeneratePdf(pdfStream);
                }
            }

            return memoryStream.ToArray();
        }

        private static IContainer CellStyle(IContainer container)
        {
            return container
                .Border(1)
                .Padding(5)
                .AlignMiddle()
                .DefaultTextStyle(style => style.FontSize(12));
        }

        private static string Escape(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return "";
            if (input.Contains(',') || input.Contains('"') || input.Contains('\n'))
                return $"\"{input.Replace("\"", "\"\"")}\"";
            return input;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "analysis/categories/leaderboards",
                    async (ISender sender) =>
                    {
                        var result = await sender.Send(new Query());

                        return result.IsFailure
                            ? Results.InternalServerError()
                            : Results.File(
                                result.Value,
                                contentType: "application/zip",
                                fileDownloadName: "pwneu-category-leaderboards.zip"
                            );
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .WithTags(nameof(Analysis));
        }
    }
}
