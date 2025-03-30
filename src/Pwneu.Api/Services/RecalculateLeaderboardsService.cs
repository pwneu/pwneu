using Dapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions.Entities;
using Pwneu.Api.Features.Announcements;
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
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AnnouncementHub>>();

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

        using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string deletePointsActivityQuery = @"DELETE FROM pwneu.""PointsActivities"";";

        const string insertSolvesQuery =
            @"
            INSERT INTO pwneu.""PointsActivities"" 
            (""UserId"", ""IsSolve"", ""ChallengeId"", ""HintId"", ""ChallengeName"", ""PointsChange"", ""OccurredAt"")
            SELECT 
                s.""UserId"",
                TRUE,
                s.""ChallengeId"",
                '00000000-0000-0000-0000-000000000000'::UUID,
                c.""Name"",
                c.""Points"",
                s.""SolvedAt""
            FROM pwneu.""Solves"" s
            JOIN pwneu.""Challenges"" c ON s.""ChallengeId"" = c.""Id"";";

        const string insertHintUsagesQuery =
            @"
            INSERT INTO pwneu.""PointsActivities"" 
            (""UserId"", ""IsSolve"", ""ChallengeId"", ""HintId"", ""ChallengeName"", ""PointsChange"", ""OccurredAt"")
            SELECT 
                hu.""UserId"",
                FALSE,
                h.""ChallengeId"",
                hu.""HintId"",
                c.""Name"",
                -h.""Deduction"",
                hu.""UsedAt""
            FROM pwneu.""HintUsages"" hu
            JOIN pwneu.""Hints"" h ON hu.""HintId"" = h.""Id""
            JOIN pwneu.""Challenges"" c ON h.""ChallengeId"" = c.""Id"";";

        const string updateUsersPointsQuery =
            @"
            UPDATE pwneu.""AspNetUsers"" u
            SET ""Points"" = COALESCE((
                SELECT SUM(""PointsChange"") 
                FROM pwneu.""PointsActivities"" pa 
                WHERE pa.""UserId"" = u.""Id""
            ), 0);";

        const string updateUsersLatestSolveQuery =
            @"
            UPDATE pwneu.""AspNetUsers"" AS u
            SET ""LatestSolve"" = sub.""LatestSolve""
            FROM (
                SELECT ""UserId"", MAX(""OccurredAt"") AS ""LatestSolve""
                FROM pwneu.""PointsActivities""
                WHERE ""IsSolve"" = TRUE
                GROUP BY ""UserId""
            ) AS sub
            WHERE u.""Id"" = sub.""UserId"";";

        try
        {
            await connection.ExecuteAsync(deletePointsActivityQuery, transaction: transaction);
            await connection.ExecuteAsync(insertSolvesQuery, transaction: transaction);
            await connection.ExecuteAsync(insertHintUsagesQuery, transaction: transaction);
            await connection.ExecuteAsync(updateUsersPointsQuery, transaction: transaction);
            await connection.ExecuteAsync(updateUsersLatestSolveQuery, transaction: transaction);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(
                "Something went wrong during the database transaction for recalculating the user's points: {Message}",
                ex.Message
            );
            throw;
        }

        await cache.RemoveAsync(CacheKeys.UserRanks(), token: cancellationToken);
        await cache.RemoveAsync(CacheKeys.TopUsersGraph(), token: cancellationToken);

        await hubContext.Clients.All.SendAsync(
            CommonConstants.ReceiveAnnouncement,
            $"Announcement:\n\nThe leaderboards has been recalculated\n\n- system",
            cancellationToken
        );

        logger.LogInformation("All user's points has been recalculated");

        return true;
    }
}
