using MediatR;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Pwneu.Api.Features.PointsActivities;

public static class DownloadLeaderboards
{
    public record Query : IRequest<Result<Document>>;

    internal sealed class Handler(AppDbContext context) : IRequestHandler<Query, Result<Document>>
    {
        public async Task<Result<Document>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var userRanks = await context.GetUserRanks(null, cancellationToken);

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
                                .Text("PWNEU Leaderboards")
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

                            column.Item().Height(1, QuestPDF.Infrastructure.Unit.Centimetre);

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
                                        header.Cell().Element(CellStyle).Text("Position").Bold();

                                        header.Cell().Element(CellStyle).Text("Username").Bold();

                                        header.Cell().Element(CellStyle).Text("Points").Bold();
                                    });

                                    var rowIndex = 0;
                                    foreach (var user in userRanks.UserRanks)
                                    {
                                        var backgroundColor =
                                            rowIndex++ % 2 == 0
                                                ? Colors.White
                                                : Colors.Grey.Lighten5;

                                        table
                                            .Cell()
                                            .Element(CellStyle)
                                            .Background(backgroundColor)
                                            .Text(user.Position.ToString());

                                        table
                                            .Cell()
                                            .Element(CellStyle)
                                            .Background(backgroundColor)
                                            .Text(user.UserName ?? "N/A");

                                        table
                                            .Cell()
                                            .Element(CellStyle)
                                            .Background(backgroundColor)
                                            .Text(user.Points.ToString());
                                    }
                                });
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                        });
                });
            });

            return document;

            static IContainer CellStyle(IContainer container)
            {
                return container
                    .Border(1)
                    .Padding(5)
                    .AlignMiddle()
                    .DefaultTextStyle(style => style.FontSize(12));
            }
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/leaderboards/download",
                    async (ISender sender) =>
                    {
                        var query = new Query();
                        var result = await sender.Send(query);

                        if (result.IsFailure)
                            return Results.BadRequest(result.Error);

                        using var stream = new MemoryStream();
                        result.Value.GeneratePdf(stream);
                        var pdfBytes = stream.ToArray();

                        return Results.File(pdfBytes, "application/pdf", "PWNEU Leaderboards.pdf");
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .RequireRateLimiting(RateLimitingPolicies.FileGeneration)
                .CacheOutput(builder => builder.Expire(TimeSpan.FromMinutes(1)))
                .WithTags(nameof(PointsActivities));
        }
    }
}
