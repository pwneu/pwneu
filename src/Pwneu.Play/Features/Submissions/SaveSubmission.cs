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

public static class SaveSubmission
{
    public record Command(
        string UserId,
        Guid ChallengeId,
        string Flag,
        DateTime SubmittedAt,
        bool IsCorrect) : IRequest<Result<Guid>>;

    private static readonly Error UserNotFound = new("SaveSubmission.UserNotFound",
        "The user with the specified ID was not found");

    private static readonly Error ChallengeNotFound = new("SaveSubmission.ChallengeNotFound",
        "The challenge with the specified ID was not found");

    private static readonly Error AlreadySolved = new("SaveSubmission.AlreadySolved",
        "The user already solved the challenge");

    internal sealed class Handler(
        ApplicationDbContext context,
        IFusionCache cache,
        IMemberAccess memberAccess)
        : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
        {
            // Check if user exists.
            var user = await memberAccess.GetMemberAsync(request.UserId, cancellationToken);

            if (user is null)
                return Result.Failure<Guid>(UserNotFound);

            // Check if the challenge still exists just in case the user has submitted
            // and the challenge was deleted at the same time.
            var challenge = await context
                .Challenges
                .Where(ch => ch.Id == request.ChallengeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (challenge is null)
                return Result.Failure<Guid>(ChallengeNotFound);

            if (request.IsCorrect)
            {
                var alreadySolved = await context
                    .Submissions
                    .AnyAsync(s =>
                        s.UserId == request.UserId &&
                        s.ChallengeId == request.ChallengeId &&
                        s.IsCorrect, cancellationToken);

                // Prevent double correct submissions.
                if (alreadySolved)
                    return Result.Failure<Guid>(AlreadySolved);
            }

            // Create a new submission and store it to the database.
            var submission = new Submission
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                UserName = user.UserName ?? "unknown",
                ChallengeId = request.ChallengeId,
                Flag = request.Flag,
                SubmittedAt = request.SubmittedAt,
                IsCorrect = request.IsCorrect,
            };

            context.Add(submission);

            if (request.IsCorrect)
            {
                challenge.SolveCount += 1;
                context.Update(challenge);
            }

            await context.SaveChangesAsync(cancellationToken);

            var invalidationTasks = new List<Task>
            {
                cache.InvalidateCategoryCacheAsync(challenge.CategoryId, cancellationToken),
            };

            if (request.IsCorrect)
            {
                invalidationTasks.Add(
                    cache.RemoveAsync(Keys.UserGraph(request.UserId), token: cancellationToken)
                        .AsTask());

                invalidationTasks.Add(
                    cache.RemoveAsync(Keys.UserSolveIds(request.UserId), token: cancellationToken)
                        .AsTask());
            }

            await Task.WhenAll(invalidationTasks);

            if (!request.IsCorrect)
                return submission.Id;

            var hasRecentRankRecount =
                await cache.GetOrDefaultAsync<bool>(Keys.HasRecentLeaderboardCount(), token: cancellationToken);

            // Querying user ranks takes a while, so we allow a slight delay of 5 seconds.
            // This gives time for the  submissions queue finish.
            // If we immediately write through the cache, the queue may not be finished yet,
            // which could result in an incorrect leaderboard.
            if (hasRecentRankRecount)
                return submission.Id;

            // Write user ranks to cache for faster retrieval.
            var userRanks = await context.GetUserRanksAsync(cancellationToken);

            await cache.SetAsync(
                Keys.UserRanks(),
                userRanks,
                new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(20) },
                cancellationToken);

            var topUsersGraph = await context.GetUsersGraphAsync(
                userRanks.Take(10).Select(u => u.Id).ToArray(),
                cancellationToken);

            await cache.SetAsync(
                Keys.TopUsersGraph(),
                topUsersGraph,
                new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(20) },
                cancellationToken);

            await cache.SetAsync(
                Keys.HasRecentLeaderboardCount(),
                true,
                new FusionCacheEntryOptions { Duration = TimeSpan.FromSeconds(5) },
                cancellationToken);

            return submission.Id;
        }
    }
}

public class SubmittedEventConsumer(ISender sender, ILogger<SubmittedEventConsumer> logger) : IConsumer<SubmittedEvent>
{
    public async Task Consume(ConsumeContext<SubmittedEvent> context)
    {
        var message = context.Message;
        var command = new SaveSubmission.Command(
            message.UserId,
            message.ChallengeId,
            message.Flag,
            message.SubmittedAt,
            message.IsCorrect);

        var result = await sender.Send(command);

        if (result.IsSuccess)
        {
            logger.LogInformation("Saved submission to database: {id}", result.Value);
            return;
        }

        logger.LogError("Failed to save submission to database: {error}", result.Error.Message);
    }
}