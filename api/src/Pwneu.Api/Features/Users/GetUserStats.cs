using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Users;

public static class GetUserStats
{
    public record Query(string Id) : IRequest<Result<UserStatsResponse>>;

    private static readonly Error NotFound = new("GetUserStats.NotFound", "User not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<UserStatsResponse>>
    {
        public async Task<Result<UserStatsResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var managerIds = await cache.GetOrSetAsync("managerIds", async _ =>
            {
                return await context
                    .UserRoles
                    .Where(ur => context.Roles
                        .Where(r =>
                            r.Name != null &&
                            (r.Name.Equals(Constants.Manager) ||
                             r.Name.Equals(Constants.Admin)))
                        .Select(r => r.Id)
                        .Contains(ur.RoleId))
                    .Select(ur => ur.UserId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            }, token: cancellationToken);

            if (managerIds.Contains(request.Id))
                return Result.Failure<UserStatsResponse>(NotFound);

            // TODO -- Get stats in categories one by one for efficient caching
            // TODO -- Invalidate cache user stats cache
            var userStats = await cache.GetOrSetAsync($"{nameof(UserStatsResponse)}:{request.Id}", async _ =>
            {
                return new UserStatsResponse(await context
                    .Categories
                    .Select(c => new CategoryEvalResponse(
                        c.Id,
                        c.Name,
                        c.Challenges.Count,
                        c.Challenges
                            .SelectMany(ch => ch.Solves)
                            .Count(s => s.UserId == request.Id),
                        c.Challenges
                            .SelectMany(ch => ch.FlagSubmissions)
                            .Count(fs => fs.UserId == request.Id && fs.FlagStatus == FlagStatus.Correct),
                        c.Challenges
                            .SelectMany(ch => ch.FlagSubmissions)
                            .Count(fs => fs.UserId == request.Id && fs.FlagStatus == FlagStatus.Incorrect)))
                    .ToListAsync(cancellationToken));
            }, token: cancellationToken);

            return userStats;
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
                .RequireAuthorization(Constants.ManagerAdminOnly)
                .WithTags(nameof(Users));
        }
    }
}