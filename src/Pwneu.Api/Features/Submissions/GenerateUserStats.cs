using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Extensions.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Submissions;

public static class GenerateUserStats
{
    public record Query(string Id) : IRequest<Result<Document>>;

    private static readonly Error NotFound = new(
        "GenerateUserStats.NotFound",
        "The user with the specified ID was not found"
    );

    internal sealed class Handler(
        AppDbContext context,
        UserManager<User> userManager,
        IFusionCache cache
    ) : IRequestHandler<Query, Result<Document>>
    {
        public async Task<Result<Document>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var user = await cache.GetUserDetailsNoEmailAsync(
                context,
                userManager,
                request.Id,
                cancellationToken
            );

            if (user is null)
                return Result.Failure<Document>(NotFound);

            var categoryEvaluations = await cache.GetUserEvaluationsAsync(
                context,
                request.Id,
                cancellationToken
            );

            var publicLeaderboardCount = await cache.GetPublicLeaderboardCountAsync(
                context,
                cancellationToken
            );

            var userRanks = await cache.GetUserRanks(
                context,
                publicLeaderboardCount,
                cancellationToken
            );

            var userRank = userRanks.UserRanks.FirstOrDefault(u => u.Id == request.Id);
            userRank ??= await cache.GetUserRankAsync(context, request.Id, 0, cancellationToken);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Column(column =>
                        {
                            column
                                .Item()
                                .Text("User Stats Report")
                                .Bold()
                                .FontSize(20)
                                .AlignCenter();

                            column
                              .Item()
                              .Text(
                                  $"User ID: {request.Id ?? "N/A"}"
                              )
                              .FontSize(10)
                              .Italic()
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
                                .Row(row =>
                                {
                                    row.RelativeItem()
                                        .Column(col =>
                                        {
                                            col.Item()
                                                .Text(text =>
                                                {
                                                    text.Span("Username: ")
                                                        .FontColor(Colors.Grey.Darken2);
                                                    text.Span(user.UserName ?? "N/A");
                                                });

                                            col.Item()
                                                .Text(text =>
                                                {
                                                    text.Span("Full Name: ")
                                                        .FontColor(Colors.Grey.Darken2);
                                                    text.Span(user.FullName ?? "N/A");
                                                });
                                        });

                                    row.RelativeItem()
                                        .Column(col =>
                                        {
                                            col.Item()
                                                .AlignRight()
                                                .Text(text =>
                                                {
                                                    text.Span("Position: ")
                                                        .FontColor(Colors.Grey.Darken2);
                                                    text.Span(
                                                        userRank?.Position.ToString() ?? "N/A"
                                                    );
                                                });

                                            col.Item()
                                                .AlignRight()
                                                .Text(text =>
                                                {
                                                    text.Span("Points: ")
                                                        .FontColor(Colors.Grey.Darken2);
                                                    text.Span(userRank?.Points.ToString() ?? "N/A");
                                                });
                                        });
                                });
                        });

                    page.Content()
                        .PaddingVertical(20)
                        .Column(content =>
                        {
                            content.Item().Text("Category Evaluations").Bold().FontSize(14);

                            if (categoryEvaluations.Count == 0)
                            {
                                content
                                    .Item()
                                    .PaddingTop(10)
                                    .Text("No category evaluations available");
                                return;
                            }

                            foreach (var category in categoryEvaluations)
                            {
                                content
                                    .Item()
                                    .PaddingVertical(10)
                                    .Border(1)
                                    .BorderColor(Colors.Grey.Lighten1)
                                    .Padding(10)
                                    .Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn();
                                            columns.RelativeColumn();
                                        });

                                        table.Cell().Text($"Category: {category.Name}");
                                        table
                                            .Cell()
                                            .Text($"Total Challenges: {category.TotalChallenges}");

                                        table.Cell().Text($"Total Solves: {category.TotalSolves}");
                                        table
                                            .Cell()
                                            .Text(
                                                $"Incorrect Attempts: {category.IncorrectAttempts}"
                                            );

                                        table.Cell().Text($"Hints Used: {category.HintsUsed}");
                                        table
                                            .Cell()
                                            .Text(text =>
                                            {
                                                text.Span("Completion Rate: ");
                                                text.Span(
                                                    (
                                                        category.TotalChallenges == 0
                                                            ? 0
                                                            : (double)category.TotalSolves
                                                                / category.TotalChallenges
                                                    ).ToString("P0")
                                                );
                                            });
                                    });
                            }
                        });
                });
            });

            return document;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/users/{id:Guid}/stats",
                    async (Guid id, ISender sender) =>
                    {
                        var query = new Query(id.ToString());
                        var result = await sender.Send(query);
                        if (result.IsFailure)
                            return Results.BadRequest(result.Error);

                        using var stream = new MemoryStream();
                        result.Value.GeneratePdf(stream);
                        var pdfBytes = stream.ToArray();

                        return Results.File(pdfBytes, "application/pdf", "User Stats Report.pdf");
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .WithTags(nameof(Submissions));

            app.MapGet(
                    "play/me/stats",
                    async (ClaimsPrincipal claims, ISender sender) =>
                    {
                        var id = claims.GetLoggedInUserId<string>();
                        if (id is null)
                            return Results.BadRequest();

                        var query = new Query(id);
                        var result = await sender.Send(query);

                        using var stream = new MemoryStream();
                        result.Value.GeneratePdf(stream);
                        var pdfBytes = stream.ToArray();

                        return Results.File(pdfBytes, "application/pdf", "User Stats Report.pdf");
                    }
                )
                .RequireAuthorization()
                .RequireRateLimiting(RateLimitingPolicies.FileGeneration)
                .WithTags(nameof(Submissions));
        }
    }
}
