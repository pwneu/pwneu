using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using System.IO.Compression;
using System.Text;

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
        public async Task<Result<byte[]>> Handle(Query request, CancellationToken cancellationToken)
        {
            var categories = await context
                .Categories.Select(c => new { c.Id, c.Name })
                .ToListAsync(cancellationToken);

            using var memoryStream = new MemoryStream();
            using var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true);

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

                var entry = zipArchive.CreateEntry($"{SanitizeFileName(category.Name)}.csv");
                await using var entryStream = entry.Open();
                var buffer = Encoding.UTF8.GetBytes(csv.ToString());
                await entryStream.WriteAsync(buffer, cancellationToken);
            }

            zipArchive.Dispose();
            return memoryStream.ToArray();
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
                            ? Results.StatusCode(500)
                            : Results.File(
                                result.Value,
                                contentType: "application/zip",
                                fileDownloadName: "category-leaderboards.zip"
                            );
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .WithTags(nameof(Analysis));
        }
    }
}
