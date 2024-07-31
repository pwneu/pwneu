using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using Pwneu.Api.Shared.Extensions;
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

            var user = await cache.GetOrSetAsync($"{nameof(UserDetailsResponse)}:{request.UserId}", async _ =>
                await context
                    .Users
                    .Where(u => u.Id == request.UserId)
                    .Select(u => new UserDetailsResponse(u.Id, u.UserName, u.Email, u.FullName, u.CreatedAt,
                        u.Solves.Sum(s => s.Challenge.Points),
                        u.FlagSubmissions.Count(fs => fs.FlagStatus == FlagStatus.Correct),
                        u.FlagSubmissions.Count(fs => fs.FlagStatus == FlagStatus.Incorrect)))
                    .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

            if (user is null) return Result.Failure<FlagStatus>(UserNotFound);

            var challenge = await cache.GetOrSetAsync($"{nameof(ChallengeDetailsResponse)}:{request.ChallengeId}",
                async _ =>
                    await context
                        .Challenges
                        .Where(c => c.Id == request.ChallengeId)
                        .Include(c => c.ChallengeFiles)
                        .Select(c => new ChallengeDetailsResponse(c.Id, c.Name, c.Description, c.Points,
                            c.DeadlineEnabled, c.Deadline, c.MaxAttempts, c.Solves.Count, c.ChallengeFiles
                                .Select(cf => new ChallengeFileResponse(cf.Id, cf.FileName))
                                .ToList()
                        ))
                        .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

            if (challenge is null) return Result.Failure<FlagStatus>(ChallengeNotFound);

            var challengeFlags = await cache.GetOrSetAsync(
                $"{nameof(Challenge)}.{nameof(Challenge.Flags)}:{request.ChallengeId}", async _ =>
                    await context
                        .Challenges
                        .Where(c => c.Id == request.ChallengeId)
                        .Select(c => c.Flags)
                        .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

            if (challengeFlags is null || challengeFlags.Count == 0)
                return Result.Failure<FlagStatus>(NoChallengeFlags);

            var attemptCountKey = $"attemptCount:{user.Id}&&{challenge.Id}";
            var solveKey = $"solve:{user.Id}&&{challenge.Id}";
            var recentSubmissionsKey = $"recentSubmissions:{user.Id}&&{challenge.Id}";

            var tenSecondsAgo = DateTime.UtcNow.AddSeconds(-10);
            int attemptCount = default;
            List<DateTime> recentSubmissions = [];
            FlagStatus flagStatus;

            if (await HasAlreadySolvedAsync())
                flagStatus = FlagStatus.AlreadySolved;
            else if (await IsSubmittingTooOftenAsync())
                flagStatus = FlagStatus.SubmittingTooOften;
            else if (challenge.DeadlineEnabled && challenge.Deadline < DateTime.Now)
                flagStatus = FlagStatus.DeadlineReached;
            else if (await IsMaxAttemptReachedAsync())
                flagStatus = FlagStatus.MaxAttemptReached;
            else if (challengeFlags.Any(f => f.Equals(request.Value)))
                flagStatus = FlagStatus.Correct;
            else flagStatus = FlagStatus.Incorrect;

            switch (flagStatus)
            {
                case FlagStatus.Correct:
                {
                    var solve = new Solve
                    {
                        UserId = user.Id,
                        ChallengeId = challenge.Id,
                        SolvedAt = DateTime.UtcNow
                    };

                    context.Solves.Add(solve);
                    await context.SaveChangesAsync(cancellationToken);

                    await cache.SetAsync(solveKey, true, token: cancellationToken);
                    await cache.SetAsync($"{nameof(ChallengeDetailsResponse)}:{request.ChallengeId}",
                        challenge with { SolveCount = challenge.SolveCount + 1 }, token: cancellationToken);
                    break;
                }
                case FlagStatus.MaxAttemptReached
                    or FlagStatus.AlreadySolved
                    or FlagStatus.DeadlineReached
                    or FlagStatus.SubmittingTooOften:
                    return flagStatus;
            }

            var flagSubmission = new FlagSubmission
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ChallengeId = challenge.Id,
                Value = request.Value,
                SubmittedAt = DateTime.UtcNow,
                FlagStatus = flagStatus,
            };

            context.FlagSubmissions.Add(flagSubmission);
            await context.SaveChangesAsync(cancellationToken);

            await cache.SetAsync(attemptCountKey, attemptCount + 1, token: cancellationToken);

            recentSubmissions.Add(flagSubmission.SubmittedAt);
            await cache.SetAsync(recentSubmissionsKey, recentSubmissions, token: cancellationToken);

            // Since a user has submitted a flag, update the cache on getting user information
            await cache.SetAsync($"{nameof(UserDetailsResponse)}:{user.Id}",
                flagStatus == FlagStatus.Correct
                    ? user with { CorrectAttempts = user.CorrectAttempts + 1 }
                    : user with { IncorrectAttempts = user.IncorrectAttempts + 1 },
                token: cancellationToken);

            return flagStatus;

            async Task<bool> HasAlreadySolvedAsync()
            {
                return await cache.GetOrSetAsync(solveKey, async _ =>
                    await context
                        .Solves
                        .AnyAsync(s => s.UserId == user.Id &&
                                       s.ChallengeId == challenge.Id, cancellationToken), token: cancellationToken);
            }

            async Task<bool> IsSubmittingTooOftenAsync()
            {
                recentSubmissions = await cache.GetOrSetAsync(recentSubmissionsKey, async _ =>
                    await context
                        .FlagSubmissions
                        .Where(fs => fs.UserId == user.Id &&
                                     fs.ChallengeId == challenge.Id &&
                                     fs.SubmittedAt >= tenSecondsAgo)
                        .Select(fs => fs.SubmittedAt)
                        .ToListAsync(cancellationToken), token: cancellationToken);

                recentSubmissions = recentSubmissions.Where(rs => rs >= tenSecondsAgo).ToList();

                return recentSubmissions.Count > 3;
            }

            async Task<bool> IsMaxAttemptReachedAsync()
            {
                attemptCount = await cache.GetOrSetAsync(attemptCountKey, async _ =>
                    await context
                        .FlagSubmissions
                        .Where(fs => fs.UserId == user.Id &&
                                     fs.ChallengeId == challenge.Id)
                        .CountAsync(cancellationToken), token: cancellationToken);

                return challenge.MaxAttempts > 0 && attemptCount >= challenge.MaxAttempts;
            }
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("challenges/{challengeId:Guid}/flags/submit",
                    async (Guid challengeId, string value, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        var command = new Command(userId!, challengeId, value);
                        var result = await sender.Send(command);

                        return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value.ToString());
                    })
                .RequireAuthorization(Constants.MemberOnly)
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