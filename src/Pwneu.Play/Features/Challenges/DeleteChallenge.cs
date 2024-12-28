using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Challenges;

/// <summary>
/// Deletes a challenge by ID.
/// Only users with manager or admin roles can access this endpoint.
/// </summary>
public static class DeleteChallenge
{
    public record Command(Guid Id, string UserId, string UserName) : IRequest<Result>;

    private static readonly Error NotFound = new("DeleteChallenge.NotFound",
        "The challenge with the specified ID was not found");

    private static readonly Error ChallengesLocked = new("DeleteChallenge.ChallengesLocked",
        "Challenges are locked. Cannot delete challenges");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, ILogger<Handler> logger)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            // The admin can bypass the challenge lock protection.
            if (!string.Equals(request.UserName, Consts.Admin, StringComparison.OrdinalIgnoreCase))
            {
                var challengesLocked = await cache.GetOrSetAsync(Keys.ChallengesLocked(), async _ =>
                        await context
                            .GetPlayConfigurationValueAsync<bool>(Consts.ChallengesLocked, cancellationToken),
                    token: cancellationToken);

                if (challengesLocked)
                    return Result.Failure(ChallengesLocked);
            }

            var challenge = await context
                .Challenges
                .Where(ch => ch.Id == request.Id)
                .Include(ch => ch.Artifacts)
                .Include(ch => ch.Hints)
                .Include(ch => ch.Submissions)
                .Include(ch => ch.Solves)
                .FirstOrDefaultAsync(cancellationToken);

            if (challenge is null) return Result.Failure(NotFound);

            context.Submissions.RemoveRange(challenge.Submissions);
            context.Solves.RemoveRange(challenge.Solves);
            context.Artifacts.RemoveRange(challenge.Artifacts);
            context.Hints.RemoveRange(challenge.Hints);
            context.Challenges.Remove(challenge);

            await context.SaveChangesAsync(cancellationToken);

            var invalidationTasks = new List<Task>
            {
                cache.InvalidateCategoryCacheAsync(challenge.CategoryId, cancellationToken: cancellationToken),
                cache.InvalidateChallengeCacheAsync(challenge, cancellationToken),
                cache.InvalidateUserGraphs(cancellationToken),
                cache.RemoveAsync(Keys.UserRanks(), token: cancellationToken).AsTask(),
                cache.RemoveAsync(Keys.TopUsersGraph(), token: cancellationToken).AsTask(),
                cache.RemoveAsync(Keys.ChallengeIds(), token: cancellationToken).AsTask()
            };

            await Task.WhenAll(invalidationTasks);

            logger.LogInformation(
                "Challenge ({Id}) deleted by {UserName} ({UserId})",
                request.Id,
                request.UserName,
                request.UserId);

            var audit = new Audit
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                UserName = request.UserName,
                Action = $"Challenge {request.Id} deleted",
                PerformedAt = DateTime.UtcNow
            };

            context.Add(audit);

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("challenges/{id:Guid}", async (Guid id, ClaimsPrincipal claims, ISender sender) =>
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
                .WithTags(nameof(Challenges));
        }
    }
}