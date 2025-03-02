using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Extensions.Entities;

public static class SolveDbExtensions
{
    public static async Task<bool> CheckIfUserHasSolvedChallengeAsync(
        this IFusionCache cache,
        AppDbContext context,
        string userId,
        Guid challengeId,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrSetAsync(
            CacheKeys.UserHasSolvedChallenge(userId, challengeId),
            async _ =>
                await context.CheckIfUserHasSolvedChallengeAsync(
                    userId,
                    challengeId,
                    cancellationToken
                ),
            new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(2) },
            cancellationToken
        );
    }

    public static async Task<bool> CheckIfUserHasSolvedChallengeAsync(
        this AppDbContext context,
        string userId,
        Guid challengeId,
        CancellationToken cancellationToken = default
    )
    {
        return await context.Solves.AnyAsync(
            s => s.UserId == userId && s.ChallengeId == challengeId,
            cancellationToken
        );
    }
}
