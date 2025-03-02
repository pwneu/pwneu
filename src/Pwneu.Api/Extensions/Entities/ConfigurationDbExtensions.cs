using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Extensions.Entities;

public static class ConfigurationDbExtensions
{
    public static async Task<bool> CheckIfSubmissionsAllowedAsync(
        this IFusionCache cache,
        AppDbContext context,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrSetAsync(
            CacheKeys.SubmissionsAllowed(),
            async _ => await context.CheckIfSubmissionsAllowedAsync(cancellationToken),
            token: cancellationToken
        );
    }

    public static async Task<bool> CheckIfSubmissionsAllowedAsync(
        this AppDbContext context,
        CancellationToken cancellationToken = default
    )
    {
        return await context.GetConfigurationValueAsync<bool>(
            ConfigurationKeys.SubmissionsAllowed,
            cancellationToken
        );
    }

    public static async Task<int> GetPublicLeaderboardCountAsync(
        this IFusionCache cache,
        AppDbContext context,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrSetAsync(
            CacheKeys.PublicLeaderboardCount(),
            async _ => await context.GetPublicLeaderboardCountAsync(cancellationToken),
            token: cancellationToken
        );
    }

    public static async Task<int> GetPublicLeaderboardCountAsync(
        this AppDbContext context,
        CancellationToken cancellationToken = default
    )
    {
        return await context.GetConfigurationValueAsync<int>(
            ConfigurationKeys.PublicLeaderboardCount,
            cancellationToken
        );
    }

    public static async Task<bool> CheckIfChallengesAreLockedAsync(
        this IFusionCache cache,
        AppDbContext context,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrSetAsync(
            CacheKeys.ChallengesLocked(),
            async _ => await context.CheckIfChallengesAreLockedAsync(cancellationToken),
            token: cancellationToken
        );
    }

    public static async Task<bool> CheckIfChallengesAreLockedAsync(
        this AppDbContext context,
        CancellationToken cancellationToken = default
    )
    {
        return await context.GetConfigurationValueAsync<bool>(
            ConfigurationKeys.ChallengesLocked,
            cancellationToken
        );
    }
}
