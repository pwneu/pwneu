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

    private static readonly Error ChallengeNotFound = new Error("SubmitFlag.ChallengeNotFound",
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

            var challenge = await cache.GetOrSetAsync($"{nameof(Challenge)}:{request.ChallengeId}", async _ =>
            {
                var challenge = await context
                    .Challenges
                    .Where(c => c.Id == request.ChallengeId)
                    .FirstOrDefaultAsync(cancellationToken);

                return challenge;
            }, token: cancellationToken);

            if (challenge is null) return Result.Failure<FlagStatus>(ChallengeNotFound);

            var attemptCountKey = $"attemptCount:{request.UserId}&&{challenge.Id}";
            var solveKey = $"solve:{request.UserId}&&{challenge.Id}";
            var recentSubmissionsKey = $"recentSubmissions:{request.UserId}&&{challenge.Id}";

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
                        UserId = request.UserId,
                        ChallengeId = challenge.Id,
                        SolvedAt = DateTime.UtcNow
                    };

                    context.Solves.Add(solve);
                    await context.SaveChangesAsync(cancellationToken);

                    await cache.SetAsync(solveKey, true, token: cancellationToken);
                    // Since a user has solved a flag, remove the cache on getting user information
                    await cache.RemoveAsync($"{nameof(UserDetailsResponse)}:{request.UserId}",
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
                Id = Guid.NewGuid(),
                UserId = request.UserId,
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
                        .AnyAsync(s => s.UserId == request.UserId && s.ChallengeId == challenge.Id, cancellationToken);
                }, token: cancellationToken);
            }

            async Task<bool> IsSubmittingTooOftenAsync()
            {
                recentSubmissions = await cache.GetOrSetAsync(recentSubmissionsKey, async _ =>
                {
                    recentSubmissions = await context
                        .FlagSubmissions
                        .Where(fs => fs.UserId == request.UserId &&
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
                        .Where(fs => fs.UserId == request.UserId && fs.ChallengeId == challenge.Id)
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
                .WithTags(nameof(Challenge));
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