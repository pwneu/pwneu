using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Extensions.Entities;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Artifacts;

public static class DeleteArtifact
{
    public record Command(Guid Id, string UserId, string UserName) : IRequest<Result>;

    private static readonly Error NotFound = new(
        "DeleteArtifact.NotFound",
        "The artifact with the specified ID was not found"
    );

    private static readonly Error ChallengesLocked = new(
        "DeleteArtifact.ChallengesLocked",
        "Challenges are locked. Cannot add artifacts"
    );

    internal sealed class Handler(AppDbContext context, IFusionCache cache, ILogger<Handler> logger)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            // The admin can bypass the challenge lock protection.
            if (!string.Equals(request.UserName, Roles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                var challengesLocked = await cache.CheckIfChallengesAreLockedAsync(
                    context,
                    cancellationToken
                );

                if (challengesLocked)
                    return Result.Failure(ChallengesLocked);
            }

            var artifact = await context
                .Artifacts.Where(a => a.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (artifact is null)
                return Result.Failure(NotFound);

            context.Artifacts.Remove(artifact);

            var audit = Audit.Create(
                request.UserId,
                request.UserName,
                $"Artifact {request.Id} removed"
            );

            context.Add(audit);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Artifact ({Id}) deleted by {UserName} ({UserId})",
                request.Id,
                request.UserName,
                request.UserId
            );

            var invalidationTasks = new List<Task>
            {
                cache
                    .RemoveAsync(
                        CacheKeys.ChallengeDetails(artifact.ChallengeId),
                        token: cancellationToken
                    )
                    .AsTask(),
                cache
                    .RemoveAsync(CacheKeys.ArtifactData(request.Id), token: cancellationToken)
                    .AsTask(),
            };

            await Task.WhenAll(invalidationTasks);

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete(
                    "play/artifacts/{id:Guid}",
                    async (Guid id, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null)
                            return Results.BadRequest();

                        var userName = claims.GetLoggedInUserName();
                        if (userName is null)
                            return Results.BadRequest();

                        var query = new Command(id, userId, userName);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.NoContent();
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .WithTags(nameof(Artifacts));
        }
    }
}
