using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Flags;

/// <summary>
/// Submits a flag and stores the submission in the database for tracking user performance.
/// Only users with a member role can access this endpoint.
/// It uses Write-through caching patterns to improve performance.
/// </summary>
public static class SubmitFlag
{
    public record Command(string UserId, Guid ChallengeId, string Value) : IRequest<Result<FlagStatus>>;

    private static readonly Error UserNotFound = new("SubmitFlag.UserNotFound",
        "The user with the specified ID was not found");

    private static readonly Error ChallengeNotFound = new("SubmitFlag.ChallengeNotFound",
        "The challenge with the specified ID was not found");

    private static readonly Error NoChallengeFlags = new("SubmitFlag.NoChallengeFlags",
        "The challenge doesn't have flags, which is weird because a challenge must have a flag :/");

    internal sealed class Handler(
        ApplicationDbContext context,
        IFusionCache cache,
        IValidator<Command> validator)
        : IRequestHandler<Command, Result<FlagStatus>>
    {
        public async Task<Result<FlagStatus>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<FlagStatus>(new Error("SubmitFlag.Validation", validationResult.ToString()));

            // Get user details in the cache or the database.
            var user = await cache.GetOrSetAsync(Keys.User(request.UserId), async _ =>
                await context
                    .Users
                    .Where(u => u.Id == request.UserId)
                    .Select(u => new UserDetailsResponse
                    {
                        Id = u.Id,
                        UserName = u.UserName,
                        Email = u.Email,
                        FullName = u.FullName,
                        CreatedAt = u.CreatedAt,
                        TotalPoints = u.Solves.Sum(s => s.Challenge.Points),
                        CorrectAttempts = u.FlagSubmissions.Count(fs => fs.FlagStatus == FlagStatus.Correct),
                        IncorrectAttempts = u.FlagSubmissions.Count(fs => fs.FlagStatus == FlagStatus.Incorrect)
                    })
                    .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

            // Check if the user exists.
            if (user is null) return Result.Failure<FlagStatus>(UserNotFound);

            // Get the challenge in the cache or the database.
            // We're using the cache of ChallengeDetailsResponse 
            // because the user has already loaded the challenge details in the cache
            // before submitting a flag.
            var challenge = await cache.GetOrSetAsync(Keys.Challenge(request.ChallengeId), async _ =>
                await context
                    .Challenges
                    .Where(c => c.Id == request.ChallengeId)
                    .Include(c => c.Artifacts)
                    .Select(c => new ChallengeDetailsResponse
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Description = c.Description,
                        Points = c.Points,
                        DeadlineEnabled = c.DeadlineEnabled,
                        Deadline = c.Deadline,
                        MaxAttempts = c.MaxAttempts,
                        SolveCount = c.Solves.Count,
                        Artifacts = c.Artifacts
                            .Select(a => new ArtifactResponse
                            {
                                Id = a.Id,
                                FileName = a.FileName,
                            })
                            .ToList()
                    })
                    .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

            // Check if the challenge exists.
            if (challenge is null) return Result.Failure<FlagStatus>(ChallengeNotFound);

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

            // Initialize cache keys (required for the methods below after the return statement).
            var attemptCountKey = $"attemptCount:{user.Id}:{challenge.Id}";
            var hasSolvedKey = $"hasSolved:{user.Id}:{challenge.Id}";
            var recentSubmissionsKey = $"recentSubmissions:{user.Id}:{challenge.Id}";

            // Cached variables for the methods below.
            int attemptCount = default;
            List<DateTime> recentSubmissions = [];
            var flagStatus = FlagStatus.Incorrect;

            // Flag value checking.
            if (await HasAlreadySolvedAsync())
                return FlagStatus.AlreadySolved;
            if (await IsSubmittingTooOftenAsync())
                return FlagStatus.SubmittingTooOften;
            if (challenge.DeadlineEnabled &&
                challenge.Deadline < DateTime.Now) // Check if the deadline has been reached.
                return FlagStatus.DeadlineReached;
            if (await IsMaxAttemptReachedAsync())
                return FlagStatus.MaxAttemptReached;
            if (challengeFlags.Any(f => f.Equals(request.Value))) // Check if the submission is correct.
            {
                flagStatus = FlagStatus.Correct;
                var solve = new Solve
                {
                    UserId = user.Id,
                    ChallengeId = challenge.Id,
                    SolvedAt = DateTime.UtcNow
                };

                // Save the submission to the Solves table.
                context.Solves.Add(solve);
                await context.SaveChangesAsync(cancellationToken);

                // Since the user has solved the challenge, update the cache of checking if already solved to true.
                await cache.SetAsync(hasSolvedKey, true, token: cancellationToken);
                // Increase the count of users who have solved the challenge in the cache.
                await cache.SetAsync(Keys.Challenge(request.ChallengeId),
                    challenge with { SolveCount = challenge.SolveCount + 1 }, token: cancellationToken);
            }

            // If no conditions have been reached, the flag status must be incorrect at this point.
            var flagSubmission = new FlagSubmission
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ChallengeId = challenge.Id,
                Value = request.Value,
                SubmittedAt = DateTime.UtcNow,
                FlagStatus = flagStatus,
            };

            // Save the submission to the database.
            context.FlagSubmissions.Add(flagSubmission);
            await context.SaveChangesAsync(cancellationToken);

            // Increase attempt count from the cache.
            await cache.SetAsync(attemptCountKey, attemptCount + 1, token: cancellationToken);

            // Add the current submission to recent submissions and update the cache.
            // Recent submissions in the cache
            // which isn't ten seconds ago will be cleaned up in the method below.
            recentSubmissions.Add(flagSubmission.SubmittedAt);
            await cache.SetAsync(recentSubmissionsKey, recentSubmissions, token: cancellationToken);

            // Since a user has submitted a flag, update the cache on getting user details.
            await cache.SetAsync(Keys.User(request.UserId),
                flagStatus == FlagStatus.Correct
                    ? user with { CorrectAttempts = user.CorrectAttempts + 1 }
                    : user with { IncorrectAttempts = user.IncorrectAttempts + 1 },
                token: cancellationToken);

            return flagStatus;

            async Task<bool> HasAlreadySolvedAsync()
            {
                return await cache.GetOrSetAsync(hasSolvedKey, async _ =>
                    await context
                        .Solves
                        .AnyAsync(s => s.UserId == user.Id &&
                                       s.ChallengeId == challenge.Id, cancellationToken), token: cancellationToken);
            }

            async Task<bool> IsSubmittingTooOftenAsync()
            {
                var tenSecondsAgo = DateTime.UtcNow.AddSeconds(-10);
                // Get all the submissions of the user where the submission date is 10 seconds ago.
                recentSubmissions = await cache.GetOrSetAsync(recentSubmissionsKey, async _ =>
                    await context
                        .FlagSubmissions
                        .Where(fs => fs.UserId == user.Id &&
                                     fs.ChallengeId == challenge.Id &&
                                     fs.SubmittedAt >= tenSecondsAgo)
                        .Select(fs => fs.SubmittedAt)
                        .ToListAsync(cancellationToken), token: cancellationToken);

                // Remove all submissions that aren't ten seconds ago.
                // This is useful when there's a cache hit.
                recentSubmissions = recentSubmissions.Where(rs => rs >= tenSecondsAgo).ToList();

                // If the user has submitted more than 3 times in the last 10 seconds,
                // that means that the user is submitting too often.
                return recentSubmissions.Count > 3;
            }

            async Task<bool> IsMaxAttemptReachedAsync()
            {
                // Update the attempt count first
                attemptCount = await cache.GetOrSetAsync(attemptCountKey, async _ =>
                    await context
                        .FlagSubmissions
                        .Where(fs => fs.UserId == user.Id &&
                                     fs.ChallengeId == challenge.Id)
                        .CountAsync(cancellationToken), token: cancellationToken);

                // If the max attempts of the challenge is 0, it means that the challenge has unlimited attempts.
                return challenge.MaxAttempts > 0 && attemptCount >= challenge.MaxAttempts;
            }
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("challenges/{challengeId:Guid}/submit",
                    async (Guid challengeId, string value, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null) return Results.BadRequest();

                        var command = new Command(userId, challengeId, value);
                        var result = await sender.Send(command);

                        return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value.ToString());
                    })
                .RequireAuthorization(Consts.MemberOnly)
                .WithTags(nameof(Flags));
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

            RuleFor(c => c.Value)
                .NotEmpty()
                .WithMessage("Flag value is required.")
                .MaximumLength(100)
                .WithMessage("Flag value must be 100 characters or less.");
        }
    }
}