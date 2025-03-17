using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Extensions.Entities;
using System.Collections.Concurrent;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Submissions;

public static class SubmitFlag
{
    public record Command(string UserId, string UserName, Guid ChallengeId, string Flag)
        : IRequest<Result<FlagStatus>>;

    private static readonly Error UserNotFound = new(
        "SubmitFlag.UserNotFound",
        "The user with the specified ID was not found"
    );

    private static readonly Error ChallengeNotFound = new(
        "SubmitFlag.ChallengeNotFound",
        "The challenge with the specified ID was not found"
    );

    private static readonly Error NoChallengeFlags = new(
        "SubmitFlag.NoChallengeFlags",
        "The challenge doesn't have flags, which is weird because a challenge must have a flag :/"
    );

    internal sealed class Handler(
        AppDbContext context,
        BufferDbContext bufferDbContext,
        IFusionCache cache,
        IValidator<Command> validator
    ) : IRequestHandler<Command, Result<FlagStatus>>
    {
        public async Task<Result<FlagStatus>> Handle(
            Command request,
            CancellationToken cancellationToken
        )
        {
            var submitTime = DateTime.UtcNow;

            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<FlagStatus>(
                    new Error("SubmitFlag.Validation", validationResult.ToString())
                );

            var userExists = await cache.CheckIfUserExistsAsync(
                context,
                request.UserId,
                cancellationToken
            );

            if (!userExists)
                return Result.Failure<FlagStatus>(UserNotFound);

            // Get the challenge in the cache or the database.
            // We're using the cache of ChallengeDetailsResponse
            // because the user might have already loaded the challenge details in the cache
            // before submitting a flag.
            var challenge = await cache.GetChallengeDetailsByIdAsync(
                context,
                request.ChallengeId,
                cancellationToken
            );

            // Cache categories first to check if the cached challenge details is still valid.
            var categories = await cache.GetCategoriesAsync(context, cancellationToken);

            // Check if the challenge exists.
            if (challenge is null)
                return Result.Failure<FlagStatus>(ChallengeNotFound);

            // Check if the category id of the challenge exists in the category.
            bool categoryExists = categories.Any(c => c.Id == challenge?.CategoryId);
            if (!categoryExists)
                return Result.Failure<FlagStatus>(ChallengeNotFound);

            // Check if there are flags in the challenge.
            if (challenge.Flags.Count == 0)
                return Result.Failure<FlagStatus>(NoChallengeFlags);

            // Check if the user has already solved the challenge.
            var hasSolved = await cache.CheckIfUserHasSolvedChallengeAsync(
                context,
                request.UserId,
                request.ChallengeId,
                cancellationToken
            );

            if (hasSolved)
                return FlagStatus.AlreadySolved;

            // Check if the submissions are allowed.
            var submissionsAllowed = await cache.CheckIfSubmissionsAllowedAsync(
                context,
                cancellationToken
            );

            if (!submissionsAllowed)
                return FlagStatus.SubmissionsNotAllowed;

            // Check if the deadline has been reached.
            if (challenge.DeadlineEnabled && challenge.Deadline < DateTime.Now)
                return FlagStatus.DeadlineReached;

            // NOTE: Since submissions are inserted into the database asynchronously,
            // there might be times when the user has more attempts than the allowed attempts
            // for the challenge.
            // This usually happens when a submission insertion is delayed long enough
            // for the user's remaining attempts in cache to expire.

            // Check how many attempts the user has left.
            int userAttemptsLeftInChallenge;
            // Check if the max attempt greater or equal to 0, meaning the attempt count has a limit.
            if (challenge.MaxAttempts > 0)
            {
                // Retrieve the current attempt count from the cache or calculate it if not present.
                var attemptCount = await cache.GetOrSetAsync(
                    CacheKeys.UserAttemptsLeftInChallenge(request.UserId, request.ChallengeId),
                    async _ =>
                        await context
                            .Submissions.Where(s =>
                                s.UserId == request.UserId && s.ChallengeId == challenge.Id
                            )
                            .CountAsync(cancellationToken),
                    token: cancellationToken
                );

                userAttemptsLeftInChallenge = challenge.MaxAttempts - attemptCount;
            }
            // If the max attempt is not 0, set the number of attempts left to infinite (max value of int).
            else
                userAttemptsLeftInChallenge = int.MaxValue;

            // Check if there are no more attempts left for the player.
            if (userAttemptsLeftInChallenge <= 0)
                return FlagStatus.MaxAttemptReached;

            // Get the recent submissions of the user in the cache,
            // if no recent submissions found, set the recentSubmits to 0.
            var userRecentSubmissionCount = await cache.GetOrDefaultAsync<int>(
                CacheKeys.UserRecentSubmissionCount(request.UserId),
                token: cancellationToken
            );

            // Don't allow brute forcing flags by checking if the user is submitting too often.
            if (userRecentSubmissionCount > 5)
                return FlagStatus.SubmittingTooOften;

            // Check if the submission is correct.
            if (challenge.Flags.Any(f => f.Equals(request.Flag)))
            {
                var correctCachingTasks = new List<Task>
                {
                    cache
                        .SetAsync(
                            CacheKeys.UserHasSolvedChallenge(request.UserId, request.ChallengeId),
                            true,
                            token: cancellationToken
                        )
                        .AsTask(),
                    cache
                        .SetAsync(
                            CacheKeys.ChallengeDetails(request.ChallengeId),
                            challenge with
                            {
                                SolveCount = challenge.SolveCount + 1,
                            },
                            token: cancellationToken
                        )
                        .AsTask(),
                    cache
                        .RemoveAsync(CacheKeys.UserGraph(request.UserId), token: cancellationToken)
                        .AsTask(),
                    cache
                        .RemoveAsync(
                            CacheKeys.UserCategoryEvaluations(request.UserId),
                            token: cancellationToken
                        )
                        .AsTask(),
                    cache
                        .RemoveAsync(
                            CacheKeys.UserRecentSubmissionCount(request.UserId),
                            token: cancellationToken
                        )
                        .AsTask(),
                };

                // Invalidate user's cache.
                await Task.WhenAll(correctCachingTasks);

                // Check if the user's solve ids were in the cache.
                var userSolvedChallengeIdsCache = await cache.GetOrDefaultAsync<List<Guid>>(
                    CacheKeys.UserSolvedChallengeIds(request.UserId),
                    token: cancellationToken
                );

                // If the cache is present, update it.
                if (userSolvedChallengeIdsCache is not null)
                {
                    userSolvedChallengeIdsCache.Add(request.ChallengeId);
                    await cache.SetAsync(
                        CacheKeys.UserSolvedChallengeIds(request.UserId),
                        userSolvedChallengeIdsCache,
                        token: cancellationToken
                    );
                }

                // Add the solved challenge to the buffer.
                var solveBuffer = SolveBuffer.Create(
                    request.UserId,
                    request.ChallengeId,
                    challenge.Name,
                    challenge.Points,
                    challenge.CategoryId,
                    submitTime
                );

                bufferDbContext.Add(solveBuffer);
                await bufferDbContext.SaveChangesAsync(cancellationToken);

                return FlagStatus.Correct;
            }

            // Condition if the submission is not correct.

            var incorrectCachingTasks = new List<Task>
            {
                // Reduce the attempt count in the cache.
                cache
                    .SetAsync(
                        CacheKeys.UserAttemptsLeftInChallenge(request.UserId, request.ChallengeId),
                        userAttemptsLeftInChallenge - 1,
                        token: cancellationToken
                    )
                    .AsTask(),
                // Increment the count of recent submissions in the cache
                // but only store the recent submissions count for 30 seconds.
                cache
                    .SetAsync(
                        CacheKeys.UserRecentSubmissionCount(request.UserId),
                        userRecentSubmissionCount + 1,
                        new FusionCacheEntryOptions { Duration = TimeSpan.FromSeconds(30) },
                        cancellationToken
                    )
                    .AsTask(),
                cache
                    .RemoveAsync(
                        CacheKeys.UserCategoryEvaluations(request.UserId),
                        token: cancellationToken
                    )
                    .AsTask(),
            };

            await Task.WhenAll(incorrectCachingTasks);

            // Add the submission to the buffer.
            var submissionBuffer = SubmissionBuffer.Create(
                request.UserId,
                request.ChallengeId,
                submitTime
            );

            bufferDbContext.Add(submissionBuffer);
            await bufferDbContext.SaveChangesAsync(cancellationToken);

            return FlagStatus.Incorrect;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> UserLocks = new();

        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "play/challenges/{challengeId:Guid}/submit",
                    async (Guid challengeId, string flag, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null)
                            return Results.BadRequest();

                        var userName = claims.GetLoggedInUserName();
                        if (userName is null)
                            return Results.BadRequest();

                        // Get or create a semaphore for the userId.
                        var userLock = UserLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));

                        // Try to acquire the lock without waiting.
                        if (!await userLock.WaitAsync(0))
                            return Results.StatusCode(StatusCodes.Status429TooManyRequests);

                        try
                        {
                            // Proceed with the command after acquiring the lock.
                            var command = new Command(userId, userName, challengeId, flag);
                            var result = await sender.Send(command);

                            return result.IsFailure
                                ? Results.NotFound(result.Error)
                                : Results.Ok(result.Value.ToString());
                        }
                        finally
                        {
                            userLock.Release();
                        }
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.MemberOnly)
                .WithTags(nameof(Submissions));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.UserId).NotEmpty().WithMessage("User ID is required.");

            RuleFor(c => c.ChallengeId).NotEmpty().WithMessage("Challenge ID is required.");

            RuleFor(c => c.Flag)
                .NotEmpty()
                .WithMessage("Flag value is required.")
                .MaximumLength(500)
                .WithMessage("Flag value must be 500 characters or less.");
        }
    }
}
