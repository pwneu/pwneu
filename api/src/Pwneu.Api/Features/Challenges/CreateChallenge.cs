using FluentValidation;
using MediatR;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.Features.Challenges;

public static class CreateChallenge
{
    public record Command(
        string Name,
        string Description,
        int Points,
        bool DeadlineEnabled,
        DateTime Deadline,
        int MaxAttempts,
        IEnumerable<string> Flags) : IRequest<Result<Guid>>;

    internal sealed class Handler(ApplicationDbContext context, IValidator<Command> validator)
        : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(new Error("CreateChallenge.Validation", validationResult.ToString()));

            var challenge = new Challenge
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                Points = request.Points,
                DeadlineEnabled = request.DeadlineEnabled,
                Deadline = request.Deadline,
                MaxAttempts = request.MaxAttempts,
                Flags = request.Flags.ToList()
            };

            context.Add(challenge);

            await context.SaveChangesAsync(cancellationToken);

            return challenge.Id;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("challenges", async (CreateChallengeRequest request, ISender sender) =>
                {
                    var command = new Command(request.Name, request.Description, request.Points,
                        request.DeadlineEnabled, request.Deadline, request.MaxAttempts, request.Flags);

                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Constants.ManagerAdminOnly)
                .WithTags(nameof(Challenge));
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
                .WithMessage("All flags must be non-empty.")
                .Must(flags => flags.All(flag => flag.Length <= 100))
                .WithMessage("Each flag must be 100 characters or less.");
        }
    }
}