using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Extensions.Entities;

public static class PointsActivitiesDbExtensions
{
    public static async Task<UserRanksResponse> GetUserRanks(
        this IFusionCache cache,
        AppDbContext context,
        int? leaderboardCount = null,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrSetAsync(
            CacheKeys.UserRanks(),
            async _ => await context.GetUserRanks(leaderboardCount, cancellationToken),
            new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(30) },
            cancellationToken
        );
    }

    public static async Task<UserRanksResponse> GetUserRanks(
        this AppDbContext context,
        int? leaderboardCount = null,
        CancellationToken cancellationToken = default
    )
    {
        var baseQuery = context.Users.Where(u => u.IsVisibleOnLeaderboards && u.Points > 0);

        var totalParticipants = await baseQuery.CountAsync(cancellationToken);

        var query = baseQuery
            .OrderByDescending(u => u.Points)
            .ThenBy(u => u.LatestSolve)
            .Select(u => new UserRankResponse
            {
                Id = u.Id,
                UserName = u.UserName ?? CommonConstants.Unknown,
                Points = u.Points,
                LatestSolve = u.LatestSolve,
            });

        if (leaderboardCount.HasValue)
            query = query.Take(leaderboardCount.Value);

        var userRanks = (await query.ToListAsync(cancellationToken))
            .Select(
                (u, index) =>
                {
                    u.Position = index + 1;
                    return u;
                }
            )
            .ToList();

        return new UserRanksResponse
        {
            UserRanks = userRanks,
            TotalParticipants = totalParticipants,
        };
    }

    public static async Task<List<UserGraphResponse>> GetUsersGraphAsync(
        this IFusionCache cache,
        AppDbContext context,
        string[] userIds,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrSetAsync(
            CacheKeys.TopUsersGraph(),
            async _ => await context.GetUsersGraphAsync(userIds, cancellationToken),
            new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(30) },
            cancellationToken
        );
    }

    public static async Task<List<UserGraphResponse>> GetUsersGraphAsync(
        this AppDbContext context,
        string[] userIds,
        CancellationToken cancellationToken = default
    )
    {
        var users = await context
            .Users.Where(u => userIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var activities = await context
            .PointsActivities.Where(pa => userIds.Contains(pa.UserId))
            .OrderBy(pa => pa.OccurredAt)
            .Select(pa => new
            {
                pa.UserId,
                pa.PointsChange,
                pa.OccurredAt,
            })
            .ToListAsync(cancellationToken);

        var userActivities = users
            .Select(u =>
            {
                int cumulativePoints = 0;

                var activityData = activities
                    .Where(a => a.UserId == u.Id)
                    .Select(a =>
                    {
                        cumulativePoints += a.PointsChange;
                        return new ActivityDataResponse
                        {
                            Score = cumulativePoints,
                            OccurredAt = a.OccurredAt,
                        };
                    })
                    .Prepend(new ActivityDataResponse { Score = 0, OccurredAt = u.CreatedAt })
                    .ToList();

                return new UserGraphResponse
                {
                    UserId = u.Id,
                    UserName = u.UserName ?? CommonConstants.Unknown,
                    Activities = activityData,
                };
            })
            .ToList();

        return userActivities;
    }

    public static async Task<UserGraphResponse> GetUserGraphAsync(
        this IFusionCache cache,
        AppDbContext context,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrSetAsync(
            CacheKeys.UserGraph(userId),
            async _ => await context.GetUserGraphAsync(userId, cancellationToken),
            new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(1) },
            cancellationToken
        );
    }

    public static async Task<UserGraphResponse> GetUserGraphAsync(
        this AppDbContext context,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var activities = await context
            .PointsActivities.Where(pa => pa.UserId == userId)
            .OrderByDescending(pa => pa.OccurredAt)
            .Take(100)
            .Select(pa => new
            {
                pa.UserId,
                pa.User.UserName,
                pa.PointsChange,
                pa.OccurredAt,
            })
            .ToListAsync(cancellationToken);

        var userGraph = new UserGraphResponse
        {
            UserId = userId,
            UserName =
                activities.Select(u => u.UserName).FirstOrDefault() ?? CommonConstants.Unknown,
        };

        var finalScore = await context
            .Users.Where(u => u.Id == userId)
            .Select(u => u.Points)
            .FirstOrDefaultAsync(cancellationToken);

        foreach (var activity in activities)
        {
            userGraph.Activities.Insert(
                0,
                new ActivityDataResponse { OccurredAt = activity.OccurredAt, Score = finalScore }
            );

            finalScore -= activity.PointsChange;
        }

        return userGraph;
    }

    public static async Task<UserRankResponse?> GetUserRankAsync(
        this IFusionCache cache,
        AppDbContext context,
        string userId,
        int position = 0,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrSetAsync(
            CacheKeys.UserRank(userId),
            async _ => await context.GetUserRankAsync(userId, position, cancellationToken),
            new FusionCacheEntryOptions { Duration = TimeSpan.FromSeconds(5) },
            cancellationToken
        );
    }

    public static async Task<UserRankResponse?> GetUserRankAsync(
        this AppDbContext context,
        string userId,
        int position = 0,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Users.Where(u => u.Id == userId)
            .Select(u => new UserRankResponse
            {
                Id = u.Id,
                UserName = u.UserName ?? CommonConstants.Unknown,
                Position = position,
                Points = u.Points,
                LatestSolve = u.LatestSolve,
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
