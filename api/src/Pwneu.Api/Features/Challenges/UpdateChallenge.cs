using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;

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
        int MaxAttempts) : IRequest<Result<Guid>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Name).NotEmpty();
            RuleFor(c => c.Description).NotEmpty();
        }
    }

    internal sealed class Handler(ApplicationDbContext context) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var challenge = await context
                .Challenges
                .Where(c => c.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (challenge is null)
                return Result.Failure(new Error("UpdateChallenge.NotFound",
                    "The challenge with the specified ID was not found"));

            challenge.Name = request.Name;
            challenge.Description = request.Description;
            challenge.Points = request.Points;
            challenge.DeadlineEnabled = request.DeadlineEnabled;
            challenge.Deadline = request.Deadline;
            challenge.MaxAttempts = request.MaxAttempts;

            context.Update(challenge);

            await context.SaveChangesAsync(cancellationToken);

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
                        request.DeadlineEnabled, request.Deadline, request.MaxAttempts);

                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization()
                .WithTags(nameof(Challenge));
        }
    }
}