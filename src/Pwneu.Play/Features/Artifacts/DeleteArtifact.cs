using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Artifacts;

public static class DeleteArtifact
{
    public record Command(Guid Id) : IRequest<Result>;

    private static readonly Error NotFound = new("DeleteArtifact.NotFound",
        "The artifact with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, ILogger<Handler> logger)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var artifact = await context
                .Artifacts
                .Include(a => a.Challenge)
                .Where(a => a.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (artifact is null)
                return Result.Failure(NotFound);

            context.Artifacts.Remove(artifact);

            await context.SaveChangesAsync(cancellationToken);

            var invalidationTasks = new List<Task>
            {
                cache.RemoveAsync(
                    Keys.ChallengeDetails(artifact.ChallengeId),
                    token: cancellationToken).AsTask(),
                cache.RemoveAsync(
                    Keys.ArtifactData(request.Id),
                    token: cancellationToken).AsTask()
            };

            await Task.WhenAll(invalidationTasks);

            logger.LogInformation("Artifact deleted: {Id}", request.Id);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("artifacts/{id:Guid}", async (Guid id, ISender sender) =>
                {
                    var query = new Command(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Artifacts));
        }
    }
}