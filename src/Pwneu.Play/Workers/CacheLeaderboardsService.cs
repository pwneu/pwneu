using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Workers;

/// <summary>
/// Worker that constantly caches leaderboards for faster queries.
/// </summary>
/// <param name="serviceProvider"></param>
/// <param name="logger"></param>
public class CacheLeaderboardsService(IServiceProvider serviceProvider, ILogger<CacheLeaderboardsService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CacheLeaderboardsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError("Something went wrong caching leaderboards {Message}", ex.Message);
            }

            await Task.Delay(3000, stoppingToken);
        }
    }

    private async Task CacheLeaderboardsAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var bufferDbContext = scope.ServiceProvider.GetRequiredService<BufferDbContext>();

        var solveBuffers = await bufferDbContext
            .SolveBuffers
            .ToListAsync(cancellationToken);

        if (solveBuffers.Count != 0)
            return;

        var appDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<IFusionCache>();

        var userRanks = await cache.GetOrSetAsync(
            Keys.UserRanks(),
            async _ => await appDbContext.GetUserRanksAsync(cancellationToken),
            new FusionCacheEntryOptions { Duration = TimeSpan.FromHours(3) },
            cancellationToken);

        // var topUsersGraph = await cache.GetOrSetAsync(Keys.TopUsersGraph(), async _ =>
        await cache.GetOrSetAsync(Keys.TopUsersGraph(), async _ =>
                await appDbContext.GetUsersGraphAsync(
                    userRanks.Take(10).Select(u => u.Id).ToArray(),
                    cancellationToken),
            token: cancellationToken);
    }
}