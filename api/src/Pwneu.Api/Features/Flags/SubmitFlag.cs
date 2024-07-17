using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using Pwneu.Api.Shared.Extensions;

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

            var challenge = await context
                .Challenges
                .Where(c => c.Id == request.ChallengeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (challenge is null)
                return Result.Failure<FlagStatus>(new Error("SubmitFlag.NullChallenge",
                    "The challenge with the specified ID was not found"));

            var user = await context
                .Users
                .Where(u => u.Id == userId)
                .FirstOrDefaultAsync(cancellationToken);

            var attemptCount = await context
                .FlagSubmissions
                .Where(fs => fs.UserId == userId && fs.ChallengeId == challenge.Id)
                .CountAsync(cancellationToken);

            FlagStatus flagStatus;

            // TODO: Block submissions that are too often (even if it's correct)

            if (await context.Solves.AnyAsync(s =>
                    s.UserId == userId && s.ChallengeId == challenge.Id, cancellationToken))
                flagStatus = FlagStatus.AlreadySolved;
            else if (challenge.DeadlineEnabled && challenge.Deadline < DateTime.Now)
                flagStatus = FlagStatus.DeadlineReached;
            else if (challenge.MaxAttempts > 0 && attemptCount >= challenge.MaxAttempts)
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
                        User = user!,
                        Challenge = challenge,
                        SolvedAt = DateTime.UtcNow
                    };

                    context.Solves.Add(solve);
                    await context.SaveChangesAsync(cancellationToken);
                    break;
                }
                case FlagStatus.MaxAttemptReached or FlagStatus.AlreadySolved or FlagStatus.DeadlineReached:
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
                Challenge = challenge,
                User = user!
            };

            context.FlagSubmissions.Add(flagSubmission);
            await context.SaveChangesAsync(cancellationToken);

            return flagStatus;
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

                    return result.IsFailure ? Results.NotFound() : Results.Ok(result.Value.ToString());
                })
                .RequireAuthorization()
                .WithTags(nameof(Challenge));
        }
    }
}