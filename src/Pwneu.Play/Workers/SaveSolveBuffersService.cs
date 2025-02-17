using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Workers;

/// <summary>
/// Worker for saving solve buffers.
/// </summary>
/// <param name="serviceProvider">The service provider.</param>
/// <param name="logger">The logger.</param>
public class SaveSolveBuffersService(IServiceProvider serviceProvider, ILogger<SaveSolveBuffersService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var buffersSaved = false;
            try
            {
                buffersSaved = await SaveSolveBuffers(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError("Something went wrong saving solve buffers {Message}", ex.Message);
            }

            if (!buffersSaved)
                await Task.Delay(1000, stoppingToken);
        }
    }

    /// <summary>
    /// Saves solve buffers in the actual database.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>If there are solve buffers found.</returns>
    private async Task<bool> SaveSolveBuffers(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var bufferDbContext = scope.ServiceProvider.GetRequiredService<BufferDbContext>();

        var solveBuffers = await bufferDbContext
            .SolveBuffers
            .ToListAsync(cancellationToken);

        if (solveBuffers.Count == 0)
            return false;

        logger.LogInformation("Saving {Count} solve buffer(s)...", solveBuffers.Count);

        var appDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<IFusionCache>();
        var memberAccess = scope.ServiceProvider.GetRequiredService<IMemberAccess>();

        var memberIds = await memberAccess.GetMemberIdsAsync(cancellationToken);

        var challengeIds = await cache.GetOrSetAsync(Keys.ChallengeIds(), async _ =>
                await appDbContext
                    .Challenges
                    .Select(ch => ch.Id)
                    .ToListAsync(cancellationToken),
            new FusionCacheEntryOptions { Duration = TimeSpan.FromSeconds(30) },
            cancellationToken);

        logger.LogInformation("Challenges count: ({ChallengeIdsCount})", challengeIds.Count);

        // Fetch the existing submissions that match the solves.
        var existingSolves = await appDbContext
            .Solves
            .Where(s => memberIds.Contains(s.UserId) && challengeIds.Contains(s.ChallengeId))
            .Select(s => new { s.UserId, s.ChallengeId })
            .ToListAsync(cancellationToken)
            .ContinueWith(t => t.Result.ToHashSet(), cancellationToken);

        // Remove all invalid solves.
        var validSolves = solveBuffers
            .Where(sb => memberIds.Contains(sb.UserId) && challengeIds.Contains(sb.ChallengeId))
            .Select(sb => new Solve
            {
                Id = sb.Id,
                UserId = sb.UserId,
                UserName = sb.UserName,
                ChallengeId = sb.ChallengeId,
                Flag = sb.Flag,
                SolvedAt = sb.SolvedAt
            })
            // Filter out existing correct submissions.
            .Where(se => !existingSolves.Contains(new { se.UserId, se.ChallengeId }))
            .GroupBy(se => new { se.UserId, se.ChallengeId })
            .Select(g => g.OrderBy(se => se.SolvedAt).First())
            .ToList();

        appDbContext.AddRange(validSolves);

        await appDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Saved {Count} solve(s)", validSolves.Count);

        // Clear the buffered solves.
        bufferDbContext.SolveBuffers.RemoveRange(solveBuffers);

        await bufferDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Cleared the stored submission buffer(s)");

        // Do not remove this commented line; it can be used for testing the normal querying of leaderboards.
        // await cache.RemoveAsync(Keys.UserRanks(), token: cancellationToken);

        // Try to get the cached leaderboards for faster recalculation.
        var userRanks = await cache.GetOrDefaultAsync<List<UserRankResponse>?>(
            Keys.UserRanks(),
            token: cancellationToken);

        // Check if someone used a hint.
        var someoneUsedHint = await cache.GetOrDefaultAsync<bool>(
            Keys.SomeoneUsedHint(),
            token: cancellationToken);

        if (someoneUsedHint)
            logger.LogInformation("Someone used a hint. Using normal querying of leaderboards");

        // If not present in the cache or someone used a hint. Just query the leaderboards normally.
        if (userRanks is null || someoneUsedHint)
        {
            logger.LogInformation("Querying leaderboards");
            userRanks = await appDbContext.GetUserRanksAsync(cancellationToken);

            await cache.SetAsync(
                Keys.UserRanks(),
                userRanks,
                new FusionCacheEntryOptions { Duration = TimeSpan.FromHours(3) },
                cancellationToken);
            logger.LogInformation("Leaderboards queried");
        }
        // If cache is present and no one used a hint, optimize the recalculation of leaderboards.
        else
        {
            logger.LogInformation("Leaderboards cache is present, optimizing recalculation of leaderboards");

            // Extract the challenge IDs of the new solves.
            var newSolveChallengeIds = validSolves
                .Select(solve => solve.ChallengeId)
                .Distinct()
                .ToList();

            // Get the points of the challenges of the new solves.
            var challengesData = await appDbContext
                .Challenges
                .Where(challenge => newSolveChallengeIds.Contains(challenge.Id))
                .Select(challenge => new
                {
                    challenge.Id,
                    challenge.Points
                })
                .ToListAsync(cancellationToken);

            // Add the points on the new solves.
            var newSolvesWithPoints = validSolves
                .Join(challengesData,
                    validSolve => validSolve.ChallengeId,
                    challengeData => challengeData.Id,
                    (validSolve, challengeData) => new
                    {
                        validSolve.UserId,
                        validSolve.UserName,
                        validSolve.SolvedAt,
                        challengeData.Points
                    });

            // Group by user with their added points.
            var usersAddedPoints = newSolvesWithPoints
                .GroupBy(newSolveWithPoints => newSolveWithPoints.UserId)
                .Select(group =>
                {
                    var firstSolve = group.First();
                    return new
                    {
                        UserId = group.Key,
                        firstSolve.UserName,
                        Points = group.Sum(newSolveWithPoints => newSolveWithPoints.Points),
                        LatestSolve = group
                            .Where(newSolveWithPoints => newSolveWithPoints.Points > 0)
                            .OrderByDescending(newSolveWithPoints => newSolveWithPoints.SolvedAt)
                            .Select(newSolveWithPoints => newSolveWithPoints.SolvedAt)
                            .FirstOrDefault()
                    };
                })
                .ToList();

            // Update the data of user ranks.
            foreach (var userAddedPoints in usersAddedPoints)
            {
                // Find the existing user in user ranks.
                var existingUser = userRanks.FirstOrDefault(u => u.Id == userAddedPoints.UserId);

                // If user not found, just add it normally.
                if (existingUser is null)
                {
                    userRanks.Add(new UserRankResponse
                    {
                        Id = userAddedPoints.UserId,
                        UserName = userAddedPoints.UserName,
                        Points = userAddedPoints.Points,
                        LatestSolve = userAddedPoints.LatestSolve,
                    });
                    continue;
                }

                // Update the points of the user.
                existingUser.Points += userAddedPoints.Points;

                // Update the latest solve.
                if (userAddedPoints.LatestSolve > existingUser.LatestSolve)
                    existingUser.LatestSolve = userAddedPoints.LatestSolve;
            }

            // Resort the user ranks and update their position.
            var newUserRanks = userRanks
                .OrderByDescending(u => u.Points)
                .ThenBy(u => u.LatestSolve)
                .Select((user, index) =>
                {
                    user.Position = index + 1;
                    return user;
                })
                .ToList();

            someoneUsedHint = await cache.GetOrDefaultAsync<bool>(
                Keys.SomeoneUsedHint(),
                token: cancellationToken);

            // If someone used a hint at this point, abort the optimization.
            if (someoneUsedHint)
            {
                logger.LogInformation("Someone used a hint, aborting optimization");
                userRanks = await appDbContext.GetUserRanksAsync(cancellationToken);

                await cache.SetAsync(
                    Keys.UserRanks(),
                    userRanks,
                    new FusionCacheEntryOptions { Duration = TimeSpan.FromHours(3) },
                    cancellationToken);
            }
            // If no one used a hint, we're safe to cache.
            else
            {
                await cache.SetAsync(
                    Keys.UserRanks(),
                    newUserRanks,
                    new FusionCacheEntryOptions { Duration = TimeSpan.FromHours(3) },
                    cancellationToken);
            }

            logger.LogInformation("Leaderboards recalculated");
        }

        // Clear the "someone used hint" cache to reenable optimization.
        await cache.RemoveAsync(Keys.SomeoneUsedHint(), token: cancellationToken);

        var topUsersGraph = await appDbContext.GetUsersGraphAsync(
            userRanks.Take(10).Select(u => u.Id).ToArray(),
            cancellationToken);

        logger.LogInformation("Querying top users' graph");

        await cache.SetAsync(
            Keys.TopUsersGraph(),
            topUsersGraph,
            new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(20) },
            cancellationToken);

        logger.LogInformation("Top users' graph queried");

        var challengeSolveCounts = validSolves
            .GroupBy(s => s.ChallengeId)
            .Select(g => new { ChallengeId = g.Key, Count = g.Count() })
            .ToList();

        foreach (var challengeSolveCount in challengeSolveCounts)
        {
            await appDbContext
                .Challenges
                .Where(ch => ch.Id == challengeSolveCount.ChallengeId)
                .ExecuteUpdateAsync(s =>
                        s.SetProperty(
                            ch => ch.SolveCount,
                            ch => ch.SolveCount + challengeSolveCount.Count),
                    cancellationToken);
        }

        logger.LogInformation("Solve count of challenges has been updated");

        return true;
    }
}