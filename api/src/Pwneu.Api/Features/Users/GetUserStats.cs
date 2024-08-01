using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Services;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Users;

public static class GetUserStats
{
    public record Query(string Id) : IRequest<Result<UserStatsResponse>>;

    private static readonly Error NotFound = new("GetUserStats.NotFound", "User not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, IAccessControl accessControl)
        : IRequestHandler<Query, Result<UserStatsResponse>>
    {
        public async Task<Result<UserStatsResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var managerIds = await accessControl.GetManagerIdsAsync(cancellationToken);

            if (managerIds.Contains(request.Id))
                return Result.Failure<UserStatsResponse>(NotFound);

            var userStats = await cache.GetOrSetAsync(Keys.UserStats(request.Id), async _ =>
                new UserStatsResponse(request.Id, await context
                    .Categories
                    .Select(c => new CategoryEvalResponse(c.Id, c.Name, c.Challenges.Count, c.Challenges
                            .SelectMany(ch => ch.Solves)
                            .Count(s => s.UserId == request.Id),
                        c.Challenges
                            .SelectMany(ch => ch.FlagSubmissions)
                            .Count(fs => fs.UserId == request.Id && fs.FlagStatus == FlagStatus.Incorrect)))
                    .ToListAsync(cancellationToken)), token: cancellationToken);

            return userStats;

            // Get all category Ids and evaluate each (multiple queries)
            // Use this just in case if the current approach is slower
#pragma warning disable CS8321
            async Task<UserStatsResponse> GetEvaluations()
#pragma warning restore CS8321
            {
                var categoryIds = await cache.GetOrSetAsync($"categoryIds", async _ =>
                    await context
                        .Categories
                        .Select(c => c.Id)
                        .ToListAsync(cancellationToken), token: cancellationToken);

                var evaluations = new List<CategoryEvalResponse>();
                foreach (var categoryId in categoryIds)
                {
                    var categoryEval = await cache.GetOrSetAsync($"category:{categoryId}:eval:{request.Id}", async _ =>
                        await context
                            .Categories
                            .Where(c => c.Id == categoryId)
                            .Select(c => new CategoryEvalResponse(c.Id, c.Name, c.Challenges.Count,
                                c.Challenges
                                    .SelectMany(ch => ch.Solves)
                                    .Count(s => s.UserId == request.Id),
                                c.Challenges
                                    .SelectMany(ch => ch.FlagSubmissions)
                                    .Count(fs => fs.UserId == request.Id && fs.FlagStatus == FlagStatus.Incorrect)))
                            .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

                    if (categoryEval is not null)
                        evaluations.Add(categoryEval);
                }

                return new UserStatsResponse(request.Id, evaluations);
            }
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/{id:Guid}/stats/",
                    async (Guid id, ISender sender) =>
                    {
                        var query = new Query(id.ToString());
                        var result = await sender.Send(query);

                        return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                    })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Users));
        }
    }
}