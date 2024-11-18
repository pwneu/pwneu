using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Artifacts;

public static class DeleteArtifact
{
    public record Command(Guid Id, string UserId, string UserName) : IRequest<Result>;

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

            logger.LogInformation(
                "Artifact ({Id}) deleted by {UserName} ({UserId})",
                request.Id,
                request.UserName,
                request.UserId);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("artifacts/{id:Guid}", async (Guid id, ClaimsPrincipal claims, ISender sender) =>
                {
                    var userId = claims.GetLoggedInUserId<string>();
                    if (userId is null) return Results.BadRequest();

                    var userName = claims.GetLoggedInUserName();
                    if (userName is null) return Results.BadRequest();

                    var query = new Command(id, userId, userName);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Artifacts));
        }
    }
}