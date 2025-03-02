using System.Text;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Services;

public class SaveBuffersService(
    IServiceProvider serviceProvider,
    IChallengePointsConcurrencyGuard guard,
    ILogger<SaveBuffersService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = serviceProvider.CreateScope();
            var bufferDbContext = scope.ServiceProvider.GetRequiredService<BufferDbContext>();

            var submissionBuffers = await bufferDbContext.SubmissionBuffers.ToListAsync(
                stoppingToken
            );
            var solveBuffers = await bufferDbContext.SolveBuffers.ToListAsync(stoppingToken);
            var hintUsageBuffers = await bufferDbContext.HintUsageBuffers.ToListAsync(
                stoppingToken
            );

            if (
                submissionBuffers.Count <= 0
                && solveBuffers.Count <= 0
                && hintUsageBuffers.Count <= 0
            )
            {
                await Task.Delay(1000, stoppingToken);
                continue;
            }

            if (!await guard.TryEnterAsync())
            {
                logger.LogInformation("Another system process is running. Retrying in 2 seconds.");
                await Task.Delay(2000, stoppingToken);
                continue;
            }

            var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cache = scope.ServiceProvider.GetRequiredService<IFusionCache>();

            var buffersSaved = false;
            try
            {
                buffersSaved = await SaveBuffers(
                    appDbContext,
                    bufferDbContext,
                    cache,
                    stoppingToken
                );
            }
            catch (Exception ex)
            {
                logger.LogError("Something went wrong saving buffers {Message}", ex.Message);
            }
            finally
            {
                guard.Exit();
            }

            if (!buffersSaved)
                await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task<bool> SaveBuffers(
        AppDbContext appDbContext,
        BufferDbContext bufferDbContext,
        IFusionCache cache,
        CancellationToken cancellationToken
    )
    {
        var submissionBuffers = await bufferDbContext.SubmissionBuffers.ToListAsync(
            cancellationToken
        );
        var solveBuffers = await bufferDbContext.SolveBuffers.ToListAsync(cancellationToken);
        var hintUsageBuffers = await bufferDbContext.HintUsageBuffers.ToListAsync(
            cancellationToken
        );

        if (submissionBuffers.Count <= 0 && solveBuffers.Count <= 0 && hintUsageBuffers.Count <= 0)
            return false;

        using var connection = new NpgsqlConnection(appDbContext.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        var sql = new StringBuilder();
        var parameters = new DynamicParameters();
        var index = 0;

        logger.LogInformation(
            "Saving {SubmissionBuffersCount} submission buffer(s), {SolveBuffersCount} solve buffer(s), and {HintUsageBuffersCount} hint usage buffer(s)...",
            submissionBuffers.Count,
            solveBuffers.Count,
            hintUsageBuffers.Count
        );

        foreach (var submissionBuffer in submissionBuffers)
        {
            var userIdParam = $"@p{index}";
            var challengeIdParam = $"@p{index + 1}";
            var submittedAtParam = $"@p{index + 2}";

            sql.AppendLine(
                $@"
                INSERT INTO pwneu.""Submissions"" (""UserId"", ""ChallengeId"", ""SubmittedAt"")
                SELECT {userIdParam}, {challengeIdParam}, {submittedAtParam}
                    WHERE EXISTS (
                SELECT 1 FROM pwneu.""AspNetUsers"" WHERE ""Id"" = {userIdParam}
                ) AND EXISTS (
                    SELECT 1 FROM pwneu.""Challenges"" WHERE ""Id"" = {challengeIdParam}
                );"
            );

            parameters.Add(userIdParam, submissionBuffer.UserId);
            parameters.Add(challengeIdParam, submissionBuffer.ChallengeId);
            parameters.Add(submittedAtParam, submissionBuffer.SubmittedAt);
            index += 3;
        }

        foreach (var solveBuffer in solveBuffers)
        {
            var userIdParam = $"@p{index}";
            var challengeIdParam = $"@p{index + 1}";
            var solvedAtParam = $"@p{index + 2}";

            sql.AppendLine(
                $@"
                INSERT INTO pwneu.""Solves"" (""UserId"", ""ChallengeId"", ""SolvedAt"")
                SELECT {userIdParam}, {challengeIdParam}, {solvedAtParam}
                WHERE EXISTS (
                    SELECT 1 FROM pwneu.""AspNetUsers"" WHERE ""Id"" = {userIdParam}
                ) AND EXISTS (
                    SELECT 1 FROM pwneu.""Challenges"" WHERE ""Id"" = {challengeIdParam}
                )
                ON CONFLICT (""UserId"", ""ChallengeId"") DO NOTHING;"
            );

            parameters.Add(userIdParam, solveBuffer.UserId);
            parameters.Add(challengeIdParam, solveBuffer.ChallengeId);
            parameters.Add(solvedAtParam, solveBuffer.SolvedAt);
            index += 3;
        }

        foreach (var hintUsageBuffer in hintUsageBuffers)
        {
            var userIdParam = $"@p{index}";
            var hintIdParam = $"@p{index + 1}";
            var usedAtParam = $"@p{index + 2}";

            sql.AppendLine(
                $@"
                INSERT INTO pwneu.""HintUsages"" (""UserId"", ""HintId"", ""UsedAt"")
                SELECT {userIdParam}, {hintIdParam}, {usedAtParam}
                WHERE EXISTS (
                    SELECT 1 FROM pwneu.""AspNetUsers"" WHERE ""Id"" = {userIdParam}
                ) AND EXISTS (
                    SELECT 1 FROM pwneu.""Hints"" WHERE ""Id"" = {hintIdParam}
                )
                ON CONFLICT (""UserId"", ""HintId"") DO NOTHING;"
            );

            parameters.Add(userIdParam, hintUsageBuffer.UserId);
            parameters.Add(hintIdParam, hintUsageBuffer.HintId);
            parameters.Add(usedAtParam, hintUsageBuffer.UsedAt);
            index += 3;
        }

        var challengeSolveCounts = solveBuffers
            .GroupBy(s => s.ChallengeId)
            .Select(group => new { ChallengeId = group.Key, SolveCount = group.Count() })
            .ToList();

        foreach (var challengeSolveCount in challengeSolveCounts)
        {
            var challengeIdParam = $"@p{index}";
            var solveCountParam = $"@p{index + 1}";

            sql.AppendLine(
                $@"
                UPDATE pwneu.""Challenges""
                SET ""SolveCount"" = ""SolveCount"" + {solveCountParam}
                WHERE ""Id"" = {challengeIdParam};"
            );

            parameters.Add(challengeIdParam, challengeSolveCount.ChallengeId);
            parameters.Add(solveCountParam, challengeSolveCount.SolveCount);
            index += 2;
        }

        var pointsActivities = new List<PointsActivity>();

        foreach (var solveBuffer in solveBuffers)
        {
            var pointsActivity = PointsActivity.CreateFromSolveBuffer(solveBuffer);
            pointsActivities.Add(pointsActivity);
        }

        foreach (var hintUsageBuffer in hintUsageBuffers)
        {
            var pointsActivity = PointsActivity.CreateFromHintUsageBuffer(hintUsageBuffer);
            pointsActivities.Add(pointsActivity);
        }

        foreach (var pointsActivity in pointsActivities)
        {
            var userIdParam = $"@p{index}";
            var isSolveParam = $"@p{index + 1}";
            var challengeIdParam = $"@p{index + 2}";
            var hintIdParam = $"@p{index + 3}";
            var challengeNameParam = $"@p{index + 4}";
            var pointsChangeParam = $"@p{index + 5}";
            var occuredAtParam = $"@p{index + 6}";

            sql.AppendLine(
                $@"
                INSERT INTO pwneu.""PointsActivities"" (""UserId"", ""IsSolve"", ""ChallengeId"", ""HintId"", ""ChallengeName"", ""PointsChange"", ""OccurredAt"")
                SELECT {userIdParam}, {isSolveParam}, {challengeIdParam}, {hintIdParam}, {challengeNameParam}, {pointsChangeParam}, {occuredAtParam}
                WHERE EXISTS (
                    SELECT 1 FROM pwneu.""AspNetUsers"" WHERE ""Id"" = {userIdParam}
                );"
            );

            parameters.Add(userIdParam, pointsActivity.UserId);
            parameters.Add(isSolveParam, pointsActivity.IsSolve);
            parameters.Add(challengeIdParam, pointsActivity.ChallengeId);
            parameters.Add(hintIdParam, pointsActivity.HintId);
            parameters.Add(challengeNameParam, pointsActivity.ChallengeName);
            parameters.Add(pointsChangeParam, pointsActivity.PointsChange);
            parameters.Add(occuredAtParam, pointsActivity.OccurredAt);
            index += 7;
        }

        var userPoints = pointsActivities
            .GroupBy(pa => pa.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                TotalPointsChange = group.Sum(pa => pa.PointsChange),
                LatestSolve = group
                    .Where(pa => pa.PointsChange > 0)
                    .Max(pa => (DateTime?)pa.OccurredAt), // Nullable to handle cases with no solve
            })
            .ToList();

        foreach (var userPoint in userPoints)
        {
            var userIdParam = $"@p{index}";
            var pointsChangeParam = $"@p{index + 1}";

            sql.AppendLine(
                $@"
                UPDATE pwneu.""AspNetUsers""
                SET ""Points"" = ""Points"" + {pointsChangeParam}
                WHERE ""Id"" = {userIdParam};"
            );

            parameters.Add(userIdParam, userPoint.UserId);
            parameters.Add(pointsChangeParam, userPoint.TotalPointsChange);
            index += 2;

            if (userPoint.LatestSolve.HasValue)
            {
                var latestSolveParam = $"@p{index}";
                sql.AppendLine(
                    $@"
                    UPDATE pwneu.""AspNetUsers""
                    SET ""LatestSolve"" = {latestSolveParam}
                    WHERE ""Id"" = {userIdParam};"
                );

                parameters.Add(latestSolveParam, userPoint.LatestSolve.Value);
                index++;
            }
        }

        // Execute the SQL
        await connection.ExecuteAsync(sql.ToString(), parameters);

        // Commit changes to the buffers
        await appDbContext.SaveChangesAsync(cancellationToken);

        bufferDbContext.RemoveRange(submissionBuffers);
        bufferDbContext.RemoveRange(solveBuffers);
        bufferDbContext.RemoveRange(hintUsageBuffers);

        await bufferDbContext.SaveChangesAsync(cancellationToken);

        // Requery leaderboards.
        var publicLeaderboardCount = await appDbContext.GetPublicLeaderboardCountAsync(
            cancellationToken
        );

        var userRanks = await appDbContext.GetUserRanks(publicLeaderboardCount, cancellationToken);

        await cache.SetAsync(
            CacheKeys.UserRanks(),
            userRanks,
            new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(30) },
            cancellationToken
        );

        var topUsersGraph = await appDbContext.GetUsersGraphAsync(
            userRanks.UserRanks.Take(10).Select(u => u.Id).ToArray(),
            cancellationToken
        );

        await cache.SetAsync(
            CacheKeys.TopUsersGraph(),
            topUsersGraph,
            new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(30) },
            cancellationToken
        );

        return true;
    }
}
