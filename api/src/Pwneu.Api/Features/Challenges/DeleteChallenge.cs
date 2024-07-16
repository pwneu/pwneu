using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;

namespace Pwneu.Api.Features.Challenges;

public static class DeleteChallenge
{
    public record Command(Guid Id) : IRequest<Result>;

    internal sealed class Handler(ApplicationDbContext context) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var challenge = await context
                .Challenges
                .Where(c => c.Id == request.Id)
                .Include(c => c.ChallengeFiles)
                .FirstOrDefaultAsync(cancellationToken);

            if (challenge is null)
                return Result.Failure(new Error("DeleteChallenge.NotFound",
                    "The challenge with the specified ID was not found"));

            context.ChallengeFiles.RemoveRange(challenge.ChallengeFiles);

            context.Challenges.Remove(challenge);

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("challenges/{id:Guid}", async (Guid id, ISender sender) =>
                {
                    var query = new Command(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization()
                .WithTags(nameof(Challenge));
        }
    }
}