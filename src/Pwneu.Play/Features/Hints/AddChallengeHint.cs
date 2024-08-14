using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Hints;

/// <summary>
/// Adds a hint to challenge.
/// Only managers and admin can access this endpoint.
/// </summary>
public static class AddChallengeHint
{
    public record Command(Guid ChallengeId, string Content, int Deduction) : IRequest<Result<Guid>>;

    private static readonly Error NoChallenge = new("AddHint.NoChallenge", "No challenge found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
        {
            var challenge = await context
                .Challenges
                .Where(c => c.Id == request.ChallengeId)
                .Include(c => c.Artifacts)
                .FirstOrDefaultAsync(cancellationToken);

            if (challenge is null)
                return Result.Failure<Guid>(NoChallenge);

            var hint = new Hint
            {
                Id = Guid.NewGuid(),
                ChallengeId = request.ChallengeId,
                Content = request.Content,
                Deduction = request.Deduction,
                Challenge = challenge,
            };

            context.Add(hint);

            await context.SaveChangesAsync(cancellationToken);

            await cache.RemoveAsync(Keys.Challenge(challenge.Id), token: cancellationToken);

            return hint.Id;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("challenges/{id:Guid}/hints",
                    async (Guid id, AddHintRequest request, ISender sender) =>
                    {
                        var command = new Command(id, request.Content, request.Deduction);

                        var result = await sender.Send(command);

                        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
                    })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Hints));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Content)
                .NotEmpty()
                .WithMessage("Challenge description is required.")
                .MaximumLength(300)
                .WithMessage("Challenge description must be 300 characters or less.");

            RuleFor(c => c.Deduction)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Deduction must be greater than or equal to 0.");
        }
    }
}