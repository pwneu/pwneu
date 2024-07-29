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

public static class SubmitFlag
{
    public record Command(string UserId, Guid ChallengeId, string Value) : IRequest<Result<FlagStatus>>;

    private static readonly Error UserNotFound = new("SubmitFlag.UserNotFound",
        "The user with the specified ID was not found");

    private static readonly Error ChallengeNotFound = new("SubmitFlag.ChallengeNotFound",
        "The challenge with the specified ID was not found");

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

            var userId = await cache.GetOrSetAsync($"{nameof(User)}:{request.UserId}", async _ =>
            {
                return await context
                    .Users
                    .Where(u => u.Id == request.UserId)
                    .Select(u => u.Id)
                    .FirstOrDefaultAsync(cancellationToken);
            }, token: cancellationToken);

            if (string.IsNullOrEmpty(userId))
                return Result.Failure<FlagStatus>(UserNotFound);

            var challenge = await cache.GetOrSetAsync($"{nameof(Challenge)}:{request.ChallengeId}", async _ =>
            {
                return await context
                    .Challenges
                    .Where(c => c.Id == request.ChallengeId)
                    .FirstOrDefaultAsync(cancellationToken);
            }, token: cancellationToken);

            if (challenge is null) return Result.Failure<FlagStatus>(ChallengeNotFound);

            var attemptCountKey = $"attemptCount:{userId}&&{challenge.Id}";
            var solveKey = $"solve:{userId}&&{challenge.Id}";
            var recentSubmissionsKey = $"recentSubmissions:{userId}&&{challenge.Id}";

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
            else if (challenge.Flags.Any(f => f.Equals(request.Value)))
                flagStatus = FlagStatus.Correct;
            else flagStatus = FlagStatus.Incorrect;

            switch (flagStatus)
            {
                case FlagStatus.Correct:
                {
                    var solve = new Solve
                    {
                        UserId = userId,
                        ChallengeId = challenge.Id,
                        SolvedAt = DateTime.UtcNow
                    };

                    context.Solves.Add(solve);
                    await context.SaveChangesAsync(cancellationToken);

                    await cache.SetAsync(solveKey, true, token: cancellationToken);
                    // Since a user has solved a flag, remove the cache on getting user information
                    await cache.RemoveAsync($"{nameof(UserDetailsResponse)}:{userId}",
                        token: cancellationToken);
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
                UserId = userId,
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

            return flagStatus;

            async Task<bool> HasAlreadySolvedAsync()
            {
                return await cache.GetOrSetAsync(solveKey, async _ =>
                {
                    return await context
                        .Solves
                        .AnyAsync(s => s.UserId == userId && s.ChallengeId == challenge.Id, cancellationToken);
                }, token: cancellationToken);
            }

            async Task<bool> IsSubmittingTooOftenAsync()
            {
                recentSubmissions = await cache.GetOrSetAsync(recentSubmissionsKey, async _ =>
                {
                    recentSubmissions = await context
                        .FlagSubmissions
                        .Where(fs => fs.UserId == userId &&
                                     fs.ChallengeId == challenge.Id &&
                                     fs.SubmittedAt >= tenSecondsAgo)
                        .Select(fs => fs.SubmittedAt)
                        .ToListAsync(cancellationToken);

                    return recentSubmissions;
                }, token: cancellationToken);

                recentSubmissions = recentSubmissions.Where(rs => rs >= tenSecondsAgo).ToList();

                return recentSubmissions.Count > 3;
            }

            async Task<bool> IsMaxAttemptReachedAsync()
            {
                attemptCount = await cache.GetOrSetAsync(attemptCountKey, async _ =>
                {
                    attemptCount = await context
                        .FlagSubmissions
                        .Where(fs => fs.UserId == userId && fs.ChallengeId == challenge.Id)
                        .CountAsync(cancellationToken);

                    return attemptCount;
                }, token: cancellationToken);

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