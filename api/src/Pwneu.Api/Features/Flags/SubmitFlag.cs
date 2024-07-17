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
    public record Command(Guid ChallengeId, string Value) : IRequest<Result<SubmitFlagResponse>>;

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
        : IRequestHandler<Command, Result<SubmitFlagResponse>>
    {
        public async Task<Result<SubmitFlagResponse>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<SubmitFlagResponse>(new Error("SubmitFlag.Validation", validationResult.ToString()));

            var userId = httpContextAccessor.HttpContext?.User.GetLoggedInUserId<string>();
            // This shouldn't happen since the user must be logged in to access this endpoint
            if (userId is null)
                return Result.Failure<SubmitFlagResponse>(new Error("SubmitFlag.NoLoggedUser", "No logged user found"));

            var challenge = await context
                .Challenges
                .Where(c => c.Id == request.ChallengeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (challenge is null)
                return Result.Failure<SubmitFlagResponse>(new Error("SubmitFlag.NullChallenge",
                    "The challenge with the specified ID was not found"));

            SubmitFlagResponse submitFlagResponse;

            // TODO: Add checking of number of attempts
            // TODO: Add checking if already solved

            if (challenge.DeadlineEnabled && challenge.Deadline < DateTime.Now)
                submitFlagResponse = SubmitFlagResponse.DeadlineReached;
            else if (challenge.Flags.Any(f => f.Equals(request.Value)))
                submitFlagResponse = SubmitFlagResponse.Correct;
            else submitFlagResponse = SubmitFlagResponse.Incorrect;

            // TODO: Record flag submission to database

            return submitFlagResponse;
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