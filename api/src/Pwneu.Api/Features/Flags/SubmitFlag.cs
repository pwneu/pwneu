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
    public record Command(Guid ChallengeId, string Value) : IRequest<Result<FlagStatus>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.ChallengeId).NotEmpty();
            RuleFor(c => c.Value).NotEmpty();
        }
    }

    internal sealed class Handler(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IFusionCache cache,
        IValidator<Command> validator)
        : IRequestHandler<Command, Result<FlagStatus>>
    {
        public async Task<Result<FlagStatus>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<FlagStatus>(new Error("SubmitFlag.Validation", validationResult.ToString()));

            var userId = httpContextAccessor.HttpContext?.User.GetLoggedInUserId<string>();
            // This shouldn't happen since the user must be logged in to access this endpoint
            if (userId is null)
                return Result.Failure<FlagStatus>(new Error("SubmitFlag.NoLoggedUser", "No logged user found"));

            var challenge = await cache.GetOrSetAsync($"{nameof(Challenge)}:{request.ChallengeId}", async _ =>
            {
                var challenge = await context
                    .Challenges
                    .Where(c => c.Id == request.ChallengeId)
                    .FirstOrDefaultAsync(cancellationToken);

                return challenge;
            }, token: cancellationToken);

            if (challenge is null)
                return Result.Failure<FlagStatus>(new Error("SubmitFlag.NullChallenge",
                    "The challenge with the specified ID was not found"));

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
                    await cache.RemoveAsync($"{nameof(UserDetailsResponse)}:{userId}", token: cancellationToken);
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
                UserId = userId,
                ChallengeId = challenge.Id,
                Value = request.Value,
                SubmittedAt = DateTime.UtcNow,
                FlagStatus = flagStatus,
            };

            context.FlagSubmissions.Add(flagSubmission);
            await context.SaveChangesAsync(cancellationToken); // TODO: Make this faster

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
            app.MapPost("challenges/{id:Guid}/flags/submit", async (Guid id, string value, ISender sender) =>
                {
                    var query = new Command(id, value);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value.ToString());
                })
                .RequireAuthorization(Policies.UserOnly)
                .WithTags(nameof(Challenge));
        }
    }
}