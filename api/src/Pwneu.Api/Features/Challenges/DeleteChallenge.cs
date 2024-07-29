using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Challenges;

/// <summary>
/// Deletes a challenge by ID.
/// Only users with manager or admin roles can access this endpoint.
/// </summary>
public static class DeleteChallenge
{
    public record Command(Guid Id) : IRequest<Result>;

    private static readonly Error NotFound = new("DeleteChallenge.NotFound",
        "The challenge with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var challenge = await context
                .Challenges
                .Where(c => c.Id == request.Id)
                .Include(c => c.ChallengeFiles)
                .FirstOrDefaultAsync(cancellationToken);

            if (challenge is null) return Result.Failure(NotFound);

            context.ChallengeFiles.RemoveRange(challenge.ChallengeFiles);

            context.Challenges.Remove(challenge);

            await context.SaveChangesAsync(cancellationToken);

            foreach (var file in challenge.ChallengeFiles)
                await cache.RemoveAsync($"{nameof(ChallengeFile)}:{file.Id}", token: cancellationToken);

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
            app.MapDelete("challenges/{id:Guid}", async (Guid id, ISender sender) =>
                {
                    var query = new Command(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Constants.ManagerAdminOnly)
                .WithTags(nameof(Challenges));
        }
    }
}