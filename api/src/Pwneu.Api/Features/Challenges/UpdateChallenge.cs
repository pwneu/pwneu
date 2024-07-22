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

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Name).NotEmpty();
            RuleFor(c => c.Description).NotEmpty();
            RuleFor(c => c.Flags).NotEmpty();
        }
    }

    internal sealed class Handler(ApplicationDbContext context, IValidator<Command> validator, IFusionCache cache)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var challenge = await context
                .Challenges
                .Where(c => c.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (challenge is null)
                return Result.Failure<Guid>(new Error("UpdateChallenge.NotFound",
                    "The challenge with the specified ID was not found"));

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
            await cache.RemoveAsync($"{nameof(ChallengeResponse)}:{challenge.Id}", token: cancellationToken);
            await cache.RemoveAsync($"{nameof(Challenge)}.{nameof(Challenge.Flags)}:{challenge.Id}",
                token: cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("challenges", async (UpdateChallengeRequest request, ISender sender) =>
                {
                    var command = new Command(request.Id, request.Name, request.Description, request.Points,
                        request.DeadlineEnabled, request.Deadline, request.MaxAttempts, request.Flags);

                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Policies.FacultyAdminOnly)
                .WithTags(nameof(Challenge));
        }
    }
}