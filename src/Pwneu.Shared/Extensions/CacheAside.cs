using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Pwneu.Shared.Extensions;

public static class CacheAside
{
    private static readonly DistributedCacheEntryOptions Default = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
    };

    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    public static async Task<T?> GetOrCreateAsync<T>(
        this IDistributedCache cache,
        string key,
        Func<CancellationToken, Task<T>> factory,
        DistributedCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        T? value;

        var cachedValue = await cache.GetStringAsync(key, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cachedValue))
        {
            value = JsonSerializer.Deserialize<T>(cachedValue);
            if (value is not null) return value;
        }

        var hasLock = await Semaphore.WaitAsync(5000, cancellationToken);

        if (!hasLock) return default;

        try
        {
            cachedValue = await cache.GetStringAsync(key, cancellationToken);
            if (!string.IsNullOrWhiteSpace(cachedValue))
            {
                value = JsonSerializer.Deserialize<T>(cachedValue);
                if (value is not null) return value;
            }

            value = await factory(cancellationToken);

            if (value is null) return default;

            await cache.SetStringAsync(key, JsonSerializer.Serialize(value), options ?? Default, cancellationToken);
        }
        finally
        {
            Semaphore.Release();
        }

        return value;
    }
}