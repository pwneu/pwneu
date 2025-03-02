using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions.Entities;
using System.Threading.Channels;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Services;

public class RecalculateLeaderboardsService(
    IServiceProvider serviceProvider,
    Channel<RecalculateRequest> channel,
    IChallengePointsConcurrencyGuard guard,
    ILogger<RecalculateLeaderboardsService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await channel.Reader.WaitToReadAsync(stoppingToken))
        {
            var request = await channel.Reader.ReadAsync(stoppingToken);
            if (request is null)
            {
                await Task.Delay(10000, stoppingToken);
                continue;
            }

            if (!await guard.TryEnterAsync())
            {
                logger.LogInformation(
                    "Another system process is running. Cancelling recalculation"
                );
                await Task.Delay(10000, stoppingToken);
                continue;
            }

            var recalculated = false;
            try
            {
                recalculated = await RecalculateAllUsersPoints(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    "Something went wrong recalculating all user's points {Message}",
                    ex.Message
                );
            }
            finally
            {
                guard.Exit();
            }

            if (!recalculated)
                await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task<bool> RecalculateAllUsersPoints(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();

        var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<IFusionCache>();

        var submissionsAllowed = await cache.CheckIfSubmissionsAllowedAsync(
            appDbContext,
            cancellationToken
        );

        if (submissionsAllowed)
        {
            logger.LogInformation(
                "Leaderboards recalculation is not allowed when submissions are enabled"
            );
            return false;
        }

        logger.LogInformation("Recalculating all user's points");

        using var connection = new NpgsqlConnection(appDbContext.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string deleteInvalidPointsActivityQuery =
            @"
            DELETE FROM pwneu.""PointsActivities""
            WHERE ""ChallengeId"" NOT IN (SELECT ""Id"" FROM pwneu.""Challenges"")
            OR (""IsSolve"" = FALSE AND ""HintId"" NOT IN (SELECT ""Id"" FROM pwneu.""Hints""));";

        const string updateUsersPointsQuery =
            @"
            UPDATE pwneu.""AspNetUsers"" u
            SET ""Points"" = COALESCE((
                SELECT SUM(""PointsChange"") 
                FROM pwneu.""PointsActivities"" pa 
                WHERE pa.""UserId"" = u.""Id""
            ), 0);";

        using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await connection.ExecuteAsync(deleteInvalidPointsActivityQuery, transaction: transaction);
        await connection.ExecuteAsync(updateUsersPointsQuery, transaction: transaction);

        await transaction.CommitAsync(cancellationToken);

        await cache.RemoveAsync(CacheKeys.UserRanks(), token: cancellationToken);
        await cache.RemoveAsync(CacheKeys.TopUsersGraph(), token: cancellationToken);

        logger.LogInformation("Recalculating all user's points has been recalculated");

        return true;
    }
}
