using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Extensions.Entities;

public static class CategoryDbExtensions
{
    public static async Task<List<CategoryResponse>> GetCategoriesAsync(
        this IFusionCache cache,
        AppDbContext context,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrSetAsync(
            CacheKeys.Categories(),
            async _ => await context.GetCategoriesAsync(cancellationToken),
            new FusionCacheEntryOptions { Duration = TimeSpan.FromHours(5) },
            cancellationToken
        );
    }

    public static async Task<List<CategoryResponse>> GetCategoriesAsync(
        this AppDbContext context,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Categories.OrderBy(c => c.Id)
            .Select(c => new CategoryResponse
            {
                Id = c.Id,
                Name = c.Name,
                ChallengesCount = c.Challenges.Count,
            })
            .ToListAsync(cancellationToken);
    }
}
