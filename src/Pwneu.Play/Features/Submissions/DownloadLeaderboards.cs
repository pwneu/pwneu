using MediatR;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Shared.Common;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Submissions;

public static class DownloadLeaderboards
{
    public record Query : IRequest<Result<Document>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<Document>>
    {
        public async Task<Result<Document>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var userRanks = await cache.GetOrSetAsync(
                Keys.UserRanks(),
                async _ => await context.GetUserRanksAsync(cancellationToken),
                new FusionCacheEntryOptions { Duration = TimeSpan.FromHours(3) },
                cancellationToken);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, QuestPDF.Infrastructure.Unit.Centimetre);

                    page.Content().Column(column =>
                    {
                        column
                            .Item()
                            .Text("PWNEU Leaderboards")
                            .FontSize(20)
                            .Bold()
                            .AlignCenter();

                        column
                            .Item()
                            .Text($"Generated on: {DateTime.UtcNow:MMMM dd, yyyy HH:mm:ss 'UTC'}")
                            .FontSize(10)
                            .Italic()
                            .AlignCenter();

                        column.Item().Height(1, QuestPDF.Infrastructure.Unit.Centimetre);

                        column.Item().Table(table =>
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
                                var backgroundColor = rowIndex++ % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

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

                    page
                        .Footer()
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

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("leaderboards/download", async (ISender sender) =>
                {
                    var query = new Query();
                    var result = await sender.Send(query);

                    using var stream = new MemoryStream();
                    result.Value.GeneratePdf(stream);
                    var pdfBytes = stream.ToArray();

                    return Results.File(pdfBytes, "application/pdf", "PWNEU Leaderboards.pdf");
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .RequireRateLimiting(Consts.Generate)
                .CacheOutput(builder => builder.Expire(TimeSpan.FromMinutes(1)))
                .WithTags(nameof(Submissions));
        }
    }
}