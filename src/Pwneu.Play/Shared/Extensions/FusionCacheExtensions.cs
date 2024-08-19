using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Shared.Extensions;

public static class FusionCacheExtensions
{
    // TODO -- Separate cache invalidations
    public static async Task InvalidateCategoryCacheAsync(
        this IFusionCache cache,
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>
        {
            cache.RemoveAsync(Keys.Categories(), token: cancellationToken).AsTask(),
            cache.RemoveAsync(Keys.CategoryIds(), token: cancellationToken).AsTask(),
            cache.RemoveAsync(Keys.Category(categoryId), token: cancellationToken).AsTask()
        };

        var activeUserIds = await cache.GetOrDefaultAsync<List<string>>(
            Keys.ActiveUserIds(),
            token: cancellationToken);

        if (activeUserIds is not null)
            tasks.AddRange(activeUserIds
                .Select(userId => cache
                    .RemoveAsync(
                        Keys.UserCategoryEval(userId, categoryId),
                        token: cancellationToken)
                    .AsTask()));

        await Task.WhenAll(tasks);
    }

    public static async Task InvalidateChallengeCacheAsync(
        this IFusionCache cache,
        Challenge challenge,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>
        {
            // TODO -- Invalidate cache Keys.Challenge() and Keys.ChallengeDetails()
            cache.RemoveAsync(
                Keys.ChallengeDetails(challenge.Id),
                token: cancellationToken).AsTask(),
            cache.RemoveAsync(
                Keys.Flags(challenge.Id),
                token: cancellationToken).AsTask(),
            cache.RemoveAsync(
                Keys.Hints(challenge.Id),
                token: cancellationToken).AsTask()
        };

        tasks.AddRange(challenge
            .Artifacts
            .Select(a =>
                cache.RemoveAsync(Keys.ArtifactData(a.Id), token: cancellationToken).AsTask()));

        tasks.AddRange(challenge
            .Hints
            .Select(h =>
                cache.RemoveAsync(Keys.Hint(h.Id), token: cancellationToken).AsTask()));

        await Task.WhenAll(tasks);
    }
}