using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Extensions.Entities;
using Razor.Templating.Core;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Submissions;

public static class GenerateUserStats
{
    public record Query(string Id) : IRequest<Result<string>>;

    private static readonly Error NotFound = new(
        "GenerateUserStats.NotFound",
        "The user with the specified ID was not found"
    );

    private static readonly Error Failed = new(
        "GenerateUserStats.Failed",
        "Failed to create certificate"
    );

    internal sealed class Handler(
        AppDbContext context,
        UserManager<User> userManager,
        IFusionCache cache
    ) : IRequestHandler<Query, Result<string>>
    {
        public async Task<Result<string>> Handle(Query request, CancellationToken cancellationToken)
        {
            var user = await cache.GetUserDetailsNoEmailAsync(
                context,
                userManager,
                request.Id,
                cancellationToken
            );

            if (user is null)
                return Result.Failure<string>(NotFound);

            var categoryEvaluations = await cache.GetUserEvaluationsAsync(
                context,
                request.Id,
                cancellationToken
            );

            var userGraph = await cache.GetUserGraphAsync(context, request.Id, cancellationToken);

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

            var userStatsReport = new Model
            {
                Id = request.Id,
                UserName = user.UserName,
                FullName = user.FullName,
                Position = userRank?.Position ?? null,
                Points = userRank?.Points ?? null,
                CategoryEvaluations = categoryEvaluations,
                UserGraph = userGraph,
                IssuedAt = DateTime.UtcNow,
            };

            var (success, userStatsReportHtml) = await RazorTemplateEngine.TryRenderPartialAsync(
                "Views/UserStatsReportView.cshtml",
                userStatsReport
            );

            return success ? userStatsReportHtml : Result.Failure<string>(Failed);
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
                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.Content(result.Value, "text/html");
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

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.Content(result.Value, "text/html");
                    }
                )
                .RequireAuthorization()
                .RequireRateLimiting(RateLimitingPolicies.FileGeneration)
                .WithTags(nameof(Submissions));
        }
    }

    public class Model
    {
        public string Id { get; init; } = default!;
        public string? UserName { get; init; }
        public string? FullName { get; init; }
        public int? Position { get; init; }
        public int? Points { get; init; }
        public List<UserCategoryEvaluationResponse> CategoryEvaluations { get; init; } = [];
        public required UserGraphResponse UserGraph { get; init; }
        public DateTime IssuedAt { get; init; }
    }
}
