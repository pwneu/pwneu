using System.Security.Claims;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Submissions;

/// <summary>
/// Submits a flag and stores the submission in the database for tracking user performance.
/// Only users with a member role can access this endpoint.
/// It uses Write-through caching patterns to improve performance.
/// </summary>
public static class SubmitFlag
{
    public record Command(string UserId, Guid ChallengeId, string Flag) : IRequest<Result<FlagStatus>>;

    private static readonly Error ChallengeNotFound = new("SubmitFlag.ChallengeNotFound",
        "The challenge with the specified ID was not found");

    private static readonly Error NoChallengeFlags = new("SubmitFlag.NoChallengeFlags",
        "The challenge doesn't have flags, which is weird because a challenge must have a flag :/");

    internal sealed class Handler(
        ApplicationDbContext context,
        IFusionCache cache,
        IPublishEndpoint publishEndpoint,
        IValidator<Command> validator)
        : IRequestHandler<Command, Result<FlagStatus>>
    {
        public async Task<Result<FlagStatus>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<FlagStatus>(new Error("SubmitFlag.Validation", validationResult.ToString()));

            // Get the challenge in the cache or the database.
            // We're using the cache of ChallengeDetailsResponse 
            // because the user might have already loaded the challenge details in the cache
            // before submitting a flag.
            var challenge = await cache.GetOrSetAsync(Keys.ChallengeDetails(request.ChallengeId), async _ =>
                await context
                    .Challenges
                    .GetDetailsByIdAsync(
                        request.ChallengeId,
                        cancellationToken), token: cancellationToken);

            // Check if the challenge exists.
            if (challenge is null)
                return Result.Failure<FlagStatus>(ChallengeNotFound);

            // Get the challenge flags in the cache or the database.
            var challengeFlags = await cache.GetOrSetAsync(Keys.Flags(request.ChallengeId), async _ =>
                await context
                    .Challenges
                    .Where(c => c.Id == request.ChallengeId)
                    .Select(c => c.Flags)
                    .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

            // Check if there are flags in the challenge.
            if (challengeFlags is null || challengeFlags.Count == 0)
                return Result.Failure<FlagStatus>(NoChallengeFlags);

            // Check if the user has already solved the challenge.
            var hasSolved = await cache.GetOrSetAsync(
                Keys.HasSolved(request.UserId, request.ChallengeId),
                async _ => await context
                    .Submissions
                    .AnyAsync(s =>
                        s.UserId == request.UserId &&
                        s.ChallengeId == request.ChallengeId &&
                        s.IsCorrect == true, cancellationToken), token: cancellationToken);

            if (hasSolved)
                return FlagStatus.AlreadySolved;

            // Check if the deadline has been reached.
            if (challenge.DeadlineEnabled && challenge.Deadline < DateTime.Now)
                return FlagStatus.DeadlineReached;

            // Check how many attempts the user has left.
            int attemptsLeft;
            // Check if the max attempt greater or equal to 0, meaning the attempt count has a limit.
            if (challenge.MaxAttempts > 0)
            {
                // Retrieve the current attempt count from the cache or calculate it if not present.
                var attemptCount = await cache.GetOrSetAsync(
                    Keys.AttemptsLeft(request.UserId, request.ChallengeId),
                    async _ => await context
                        .Submissions
                        .Where(s =>
                            s.UserId == request.UserId &&
                            s.ChallengeId == challenge.Id)
                        .CountAsync(cancellationToken), token: cancellationToken);

                attemptsLeft = challenge.MaxAttempts - attemptCount;
            }
            // If the max attempt is not 0, set the number of attempts left to infinite (max value of int).
            else attemptsLeft = int.MaxValue;

            // Check if there are no more attempts left for the player.
            if (attemptsLeft <= 0)
                return FlagStatus.MaxAttemptReached;

            // Get the recent submissions of the user in the cache,
            // if no recent submissions found, set the recentSubmits to 0.
            var recentSubmits = await cache.GetOrDefaultAsync<int>(
                Keys.RecentSubmits(request.UserId, request.ChallengeId),
                token: cancellationToken);

            // Don't allow brute forcing flags by checking if the user is submitting too often.
            if (recentSubmits > 5)
                return FlagStatus.SubmittingTooOften;

            var flagStatus = FlagStatus.Incorrect;

            // Check if the submission is correct.
            if (challengeFlags.Any(f => f.Equals(request.Flag)))
            {
                flagStatus = FlagStatus.Correct;

                // Since the user has solved the challenge, update the cache of checking if already solved to true.
                await cache.SetAsync(
                    Keys.HasSolved(request.UserId, request.ChallengeId),
                    true,
                    token: cancellationToken);

                // Increase the count of users who have solved the challenge in the cache.
                await cache.SetAsync(Keys.ChallengeDetails(request.ChallengeId),
                    challenge with { SolveCount = challenge.SolveCount + 1 }, token: cancellationToken);
            }
            else // Condition if the submission is not correct.
            {
                // Reduce the attempt count in the cache.
                await cache.SetAsync(
                    Keys.AttemptsLeft(request.UserId, request.ChallengeId),
                    attemptsLeft - 1,
                    token: cancellationToken);

                // Increment the count of recent submissions in the cache
                // but only store the recent submissions count for 20 seconds.
                await cache.SetAsync(
                    Keys.RecentSubmits(request.UserId, request.ChallengeId),
                    recentSubmits + 1,
                    new FusionCacheEntryOptions { Duration = TimeSpan.FromSeconds(20) },
                    cancellationToken);
            }

            // Create a queue on for storing the submission on the database.
            await publishEndpoint.Publish(new SubmittedEvent
            {
                UserId = request.UserId,
                ChallengeId = request.ChallengeId,
                Flag = request.Flag,
                SubmittedAt = DateTime.UtcNow,
                IsCorrect = flagStatus == FlagStatus.Correct,
            }, cancellationToken);

            return flagStatus;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("challenges/{challengeId:Guid}/submit",
                    async (Guid challengeId, string flag, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null) return Results.BadRequest();

                        var command = new Command(userId, challengeId, flag);
                        var result = await sender.Send(command);

                        return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value.ToString());
                    })
                .RequireAuthorization(Consts.MemberOnly)
                .WithTags(nameof(Submissions));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.UserId)
                .NotEmpty()
                .WithMessage("User ID is required.");

            RuleFor(c => c.ChallengeId)
                .NotEmpty()
                .WithMessage("Challenge ID is required.");

            RuleFor(c => c.Flag)
                .NotEmpty()
                .WithMessage("Flag value is required.")
                .MaximumLength(100)
                .WithMessage("Flag value must be 100 characters or less.");
        }
    }
}