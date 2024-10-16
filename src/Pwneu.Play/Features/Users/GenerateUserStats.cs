using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Play.Shared.Services;
using Pwneu.Play.Views;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using Razor.Templating.Core;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Users;

public static class GenerateUserStats
{
    public record Query(string Id) : IRequest<Result<string>>;

    private static readonly Error NotFound = new(
        "GenerateUserStats.NotFound",
        "The user with the specified ID was not found");

    private static readonly Error Failed = new(
        "GenerateUserStats.Failed",
        "Failed to create certificate");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, IMemberAccess memberAccess)
        : IRequestHandler<Query, Result<string>>
    {
        public async Task<Result<string>> Handle(Query request, CancellationToken cancellationToken)
        {
            // Check if user exists.
            var user = await memberAccess.GetMemberDetailsAsync(request.Id, cancellationToken);

            if (user is null)
                return Result.Failure<string>(NotFound);

            // Get all the categories first.
            var categoryIds = await cache.GetOrSetAsync(Keys.CategoryIds(), async _ =>
                await context
                    .Categories
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken), token: cancellationToken);

            var userCategoryEvaluations = new List<UserCategoryEvalResponse>();

            // Cache each user category evaluation.
            foreach (var categoryId in categoryIds)
            {
                var userCategoryEvaluation = await cache.GetOrSetAsync(
                    Keys.UserCategoryEval(request.Id, categoryId),
                    async _ => await context
                        .Categories
                        .GetUserEvaluationInCategoryAsync(request.Id, categoryId, cancellationToken),
                    token: cancellationToken);

                if (userCategoryEvaluation is not null)
                    userCategoryEvaluations.Add(userCategoryEvaluation);
            }

            var userGraph = await cache.GetOrSetAsync(Keys.UserGraph(request.Id),
                async _ =>
                {
                    var userGraph = await context.GetUserGraphAsync(request.Id, cancellationToken);
                    return userGraph;
                },
                token: cancellationToken);

            var userRanks = await cache.GetOrSetAsync(
                Keys.UserRanks(),
                async _ => await context.GetUserRanksAsync(cancellationToken),
                new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(20) },
                cancellationToken);

            var userRank = userRanks.FirstOrDefault(u => u.Id == request.Id);

            var activeUserIds = await cache.GetOrDefaultAsync<List<string>>(
                Keys.ActiveUserIds(),
                token: cancellationToken) ?? [];

            if (!activeUserIds.Contains(request.Id))
                activeUserIds.Add(request.Id);

            // Store the userId to the cache for easier invalidations.
            await cache.SetAsync(
                Keys.ActiveUserIds(),
                activeUserIds, token: cancellationToken);

            var userStatsReport = new UserStatsReport
            {
                Id = user.Id,
                UserName = user.UserName,
                FullName = user.FullName,
                Position = userRank?.Position ?? null,
                Points = userRank?.Points ?? null,
                CategoryEvaluations = userCategoryEvaluations,
                UserGraph = userGraph,
                IssuedAt = DateTime.UtcNow
            };

            var (success, userStatsReportHtml) = await RazorTemplateEngine.TryRenderPartialAsync(
                "Views/UserStatsReportView.cshtml",
                userStatsReport);

            return success ? userStatsReportHtml : Result.Failure<string>(Failed);
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/{id:Guid}/stats", async (Guid id, ISender sender) =>
                {
                    var query = new Query(id.ToString());
                    var result = await sender.Send(query);

                    return result.IsFailure
                        ? Results.NotFound(result.Error)
                        : Results.Content(result.Value, "text/html");
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Users));

            app.MapGet("me/stats", async (ClaimsPrincipal claims, ISender sender) =>
                {
                    var id = claims.GetLoggedInUserId<string>();
                    if (id is null) return Results.BadRequest();

                    var query = new Query(id);
                    var result = await sender.Send(query);

                    return result.IsFailure
                        ? Results.NotFound(result.Error)
                        : Results.Content(result.Value, "text/html");
                })
                .RequireAuthorization()
                .RequireRateLimiting(Consts.Generate)
                .WithTags(nameof(Users));
        }
    }
}