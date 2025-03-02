using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Extensions.Entities;

public static class HintUsageDbExtensions
{
    public static async Task<bool> CheckIfUserHasUsedHintAsync(
        this IFusionCache cache,
        AppDbContext context,
        string userId,
        Guid hintId,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrSetAsync(
            CacheKeys.UserHasUsedHint(userId, hintId),
            async _ => await context.CheckIfUserHasUsedHintAsync(userId, hintId, cancellationToken),
            new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(2) },
            token: cancellationToken
        );
    }

    public static async Task<bool> CheckIfUserHasUsedHintAsync(
        this AppDbContext context,
        string userId,
        Guid hintId,
        CancellationToken cancellationToken = default
    )
    {
        return await context.HintUsages.AnyAsync(
            s => s.UserId == userId && s.HintId == hintId,
            cancellationToken
        );
    }
}
