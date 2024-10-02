using System.Security.Claims;
using MediatR;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Submissions;

/// <summary>
/// Gets the leaderboards.
/// Shows full leaderboards if manager admin.
/// Shows top users if member.
/// </summary>
public static class GetLeaderboards
{
    public record Query(string RequesterId, bool IsMember) : IRequest<Result<LeaderboardsResponse>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<LeaderboardsResponse>>
    {
        public async Task<Result<LeaderboardsResponse>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var userRanks = await cache.GetOrSetAsync(
                Keys.UserRanks(),
                async _ => await context.GetUserRanks(cancellationToken),
                new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(20) },
                cancellationToken);

            var requesterRank = userRanks.FirstOrDefault(u => u.Id == request.RequesterId);

            var publicLeaderboardCount = await cache.GetOrSetAsync(Keys.PublicLeaderboardCount(),
                async _ => await context.GetPlayConfigurationValueAsync<int>(
                    Consts.PublicLeaderboardCount,
                    cancellationToken),
                token: cancellationToken);

            var topUsersGraph = await cache.GetOrSetAsync(Keys.TopUsersGraph(), async _ =>
                    await context.GetUsersGraph(
                        userRanks.Take(10).Select(u => u.Id).ToArray(),
                        cancellationToken),
                token: cancellationToken);

            // Only show top users the requester is a member.
            if (request.IsMember)
                userRanks = userRanks.Take(publicLeaderboardCount).ToList();

            return new LeaderboardsResponse
            {
                RequesterRank = requesterRank,
                UserRanks = userRanks,
                TopUsersGraph = topUsersGraph,
                RequesterIsMember = request.IsMember,
                PublicLeaderboardCount = publicLeaderboardCount
            };
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("leaderboards", async (ClaimsPrincipal claims, ISender sender) =>
                {
                    var userId = claims.GetLoggedInUserId<string>();
                    if (userId is null)
                        return Results.BadRequest();

                    var isMember = claims.GetRoles().Contains(Consts.Member);

                    var query = new Query(userId, isMember);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.StatusCode(500) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .WithTags(nameof(Submissions));
        }
    }
}