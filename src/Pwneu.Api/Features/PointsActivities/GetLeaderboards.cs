using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions;
using Pwneu.Api.Extensions.Entities;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.PointsActivities;

public static class GetLeaderboards
{
    public record Query(string RequesterId, int? LeaderboardCount, bool IsMember)
        : IRequest<Result<LeaderboardsResponse>>;

    internal sealed class Handler(AppDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<LeaderboardsResponse>>
    {
        public async Task<Result<LeaderboardsResponse>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            UserRanksResponse userRanks;
            List<UserGraphResponse> topUsersGraph;

            if (request.LeaderboardCount is not null && !request.IsMember)
            {
                userRanks = await context.GetUserRanks(
                    Math.Max(request.LeaderboardCount.Value, 10),
                    cancellationToken
                );

                if (userRanks.TotalParticipants == 0)
                    return new LeaderboardsResponse();

                // Custom leaderboard count requests are not cached.
                topUsersGraph = await context.GetUsersGraphAsync(
                    userRanks.UserRanks.Take(10).Select(u => u.Id).ToArray(),
                    cancellationToken
                );
            }
            else
            {
                var publicLeaderboardCount = await cache.GetPublicLeaderboardCountAsync(
                    context,
                    cancellationToken
                );

                userRanks = await cache.GetUserRanks(
                    context,
                    publicLeaderboardCount,
                    cancellationToken
                );

                if (userRanks.TotalParticipants == 0)
                    return new LeaderboardsResponse();

                topUsersGraph = await cache.GetUsersGraphAsync(
                    context,
                    userRanks.UserRanks.Take(10).Select(u => u.Id).ToArray(),
                    cancellationToken
                );
            }

            var requesterRank = request.IsMember
                ? userRanks.UserRanks.FirstOrDefault(u => u.Id == request.RequesterId)
                : null;

            return new LeaderboardsResponse
            {
                RequesterRank = requesterRank,
                UserRanks = userRanks.UserRanks,
                RequesterIsMember = request.IsMember,
                PublicLeaderboardCount = 0,
                TopUsersGraph = topUsersGraph,
                TotalLeaderboardCount = userRanks.TotalParticipants,
            };
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/leaderboards",
                    async (int? count, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null)
                            return Results.BadRequest();

                        var isMember = claims.GetRoles().Contains(Roles.Member);

                        var query = new Query(userId, count, isMember);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.StatusCode(500)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization()
                .RequireRateLimiting(RateLimitingPolicies.Fixed)
                .WithTags(nameof(PointsActivities));
        }
    }
}
