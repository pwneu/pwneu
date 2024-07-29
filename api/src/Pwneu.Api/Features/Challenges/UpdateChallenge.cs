using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Challenges;

public static class UpdateChallenge
{
    public record Command(
        Guid Id,
        string Name,
        string Description,
        int Points,
        bool DeadlineEnabled,
        DateTime Deadline,
        int MaxAttempts,
        IEnumerable<string> Flags) : IRequest<Result>;

    private static readonly Error NotFound = new("UpdateChallenge.NotFound",
        "The challenge with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IValidator<Command> validator, IFusionCache cache)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var challenge = await context
                .Challenges
                .Where(c => c.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (challenge is null) return Result.Failure<Guid>(NotFound);

            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(new Error("UpdateChallenge.Validation", validationResult.ToString()));

            challenge.Name = request.Name;
            challenge.Description = request.Description;
            challenge.Points = request.Points;
            challenge.DeadlineEnabled = request.DeadlineEnabled;
            challenge.Deadline = request.Deadline;
            challenge.MaxAttempts = request.MaxAttempts;
            challenge.Flags = request.Flags.ToList();

            context.Update(challenge);

            await context.SaveChangesAsync(cancellationToken);

            await cache.RemoveAsync($"{nameof(Challenge)}:{challenge.Id}", token: cancellationToken);
            await cache.RemoveAsync($"{nameof(ChallengeDetailsResponse)}:{challenge.Id}", token: cancellationToken);
            await cache.RemoveAsync($"{nameof(Challenge)}.{nameof(Challenge.Flags)}:{challenge.Id}",
                token: cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("challenges/{id:Guid}", async (Guid id, UpdateChallengeRequest request, ISender sender) =>
                {
                    var command = new Command(id, request.Name, request.Description, request.Points,
                        request.DeadlineEnabled, request.Deadline, request.MaxAttempts, request.Flags);

                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Constants.ManagerAdminOnly)
                .WithTags(nameof(Challenges));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Name)
                .NotEmpty()
                .WithMessage("Challenge name is required.")
                .MaximumLength(100)
                .WithMessage("Challenge name must be 100 characters or less.");

            RuleFor(c => c.Description)
                .NotEmpty()
                .WithMessage("Challenge description is required.")
                .MaximumLength(300)
                .WithMessage("Challenge description must be 300 characters or less.");

            RuleFor(c => c.Points)
                .GreaterThan(0)
                .WithMessage("Points must be greater than 0.");

            RuleFor(c => c.MaxAttempts)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Max attempts must be greater than or equal to 0.");

            RuleFor(c => c.Flags)
                .NotNull()
                .WithMessage("Flags are required.")
                .NotEmpty()
                .WithMessage("Flags cannot be empty.")
                .Must(flags => flags.All(flag => !string.IsNullOrWhiteSpace(flag)))
                .WithMessage("All flags must be non-empty.");
        }
    }
}