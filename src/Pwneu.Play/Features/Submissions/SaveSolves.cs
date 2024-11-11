using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Submissions;

/// <summary>
/// Cache leaderboards for faster query.
/// </summary>
public static class SaveSolves
{
    public record Command(List<SolvedEvent> SolvedEvents) : IRequest<Result>;

    internal sealed class Handler(
        ApplicationDbContext context,
        IFusionCache cache,
        IMemberAccess memberAccess,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var memberIds = await memberAccess.GetMemberIdsAsync(cancellationToken);

            var challengeIds = await cache.GetOrSetAsync(Keys.ChallengeIds(), async _ =>
                    await context
                        .Challenges
                        .Select(ch => ch.Id)
                        .ToListAsync(cancellationToken),
                new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(20) },
                cancellationToken);

            // Fetch the existing submissions that match the solves.
            var existingCorrectSubmissions = await context
                .Solves
                .Where(s => memberIds.Contains(s.UserId) && challengeIds.Contains(s.ChallengeId))
                .Select(s => new { s.UserId, s.ChallengeId })
                .ToListAsync(cancellationToken)
                .ContinueWith(t => t.Result.ToHashSet(), cancellationToken);

            // Remove all invalid solves.
            var validSolves = request
                .SolvedEvents
                .Where(se => memberIds.Contains(se.UserId) && challengeIds.Contains(se.ChallengeId))
                .Select(se => new Solve
                {
                    Id = Guid.NewGuid(),
                    UserId = se.UserId,
                    UserName = se.UserName,
                    ChallengeId = se.ChallengeId,
                    Flag = se.Flag,
                    SolvedAt = se.SolvedAt
                })
                // Filter out existing correct submissions.
                .Where(se => !existingCorrectSubmissions.Contains(new { se.UserId, se.ChallengeId }))
                .GroupBy(se => new { se.UserId, se.ChallengeId })
                .Select(g => g.OrderBy(se => se.SolvedAt).First())
                .ToList();

            context.AddRange(validSolves);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Saved {Count} solves", validSolves.Count);

            var hasRecentRankRecount =
                await cache.GetOrDefaultAsync<bool>(Keys.HasRecentLeaderboardCount(), token: cancellationToken);

            logger.LogInformation("Recalculating leaderboards");

            // Caching user ranks takes a while, so we allow a slight delay of 5 seconds (IGNORE THIS COMMENT).
            if (!hasRecentRankRecount)
            {
                var userRanks = await context.GetUserRanksAsync(cancellationToken);

                await cache.SetAsync(
                    Keys.UserRanks(),
                    userRanks,
                    new FusionCacheEntryOptions { Duration = TimeSpan.FromHours(3) },
                    cancellationToken);

                var topUsersGraph = await context.GetUsersGraphAsync(
                    userRanks.Take(10).Select(u => u.Id).ToArray(),
                    cancellationToken);

                await cache.SetAsync(
                    Keys.TopUsersGraph(),
                    topUsersGraph,
                    new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(20) },
                    cancellationToken);

                // await cache.SetAsync(
                //     Keys.HasRecentLeaderboardCount(),
                //     true,
                //     new FusionCacheEntryOptions { Duration = TimeSpan.FromSeconds(5) },
                //     cancellationToken);
            }

            logger.LogInformation("Leaderboards recalculated");

            var challengeSolveCounts = validSolves
                .GroupBy(s => s.ChallengeId)
                .Select(g => new { ChallengeId = g.Key, Count = g.Count() })
                .ToList();

            foreach (var challengeSolveCount in challengeSolveCounts)
            {
                await context.Challenges
                    .Where(ch => ch.Id == challengeSolveCount.ChallengeId)
                    .ExecuteUpdateAsync(s =>
                            s.SetProperty(
                                ch => ch.SolveCount,
                                ch => ch.SolveCount + challengeSolveCount.Count),
                        cancellationToken);
            }

            logger.LogInformation("Solve count of challenges has been updated");

            return Result.Success();
        }
    }
}

public class SolvedEventsConsumer(ISender sender, ILogger<SolvedEventsConsumer> logger)
    : IConsumer<Batch<SolvedEvent>>
{
    public async Task Consume(ConsumeContext<Batch<SolvedEvent>> context)
    {
        var solvedEvents = context
            .Message
            .Select(s => s.Message)
            .ToList();

        var command = new SaveSolves.Command(solvedEvents);

        var result = await sender.Send(command);

        if (result.IsSuccess)
        {
            logger.LogInformation("Successfully saved solve");
            return;
        }

        logger.LogError("Failed to save solve: {error}", result.Error.Message);
    }
}