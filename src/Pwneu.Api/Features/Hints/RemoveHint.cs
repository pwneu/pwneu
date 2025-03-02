using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Extensions.Entities;
using Pwneu.Api.Services;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Hints;

public static class RemoveHint
{
    public record Command(Guid Id, string UserId, string UserName) : IRequest<Result>;

    private static readonly Error NotFound = new(
        "DeleteHint.NotFound",
        "The hint with the specified ID was not found"
    );

    private static readonly Error ChallengesLocked = new(
        "RemoveHint.ChallengesLocked",
        "Challenges are locked. Cannot remove hints"
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
                    return Result.Failure<Guid>(ChallengesLocked);
            }

            var hint = await context
                .Hints.Where(h => h.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (hint is null)
                return Result.Failure(NotFound);

            context.Remove(hint);

            var audit = Audit.Create(
                request.UserId,
                request.UserName,
                $"Hint {request.Id} removed"
            );

            context.Add(audit);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Hint ({HintId}) removed by {UserName} ({UserId})",
                request.Id,
                request.UserName,
                request.UserId
            );

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete(
                    "play/hints/{id:Guid}",
                    async (
                        Guid id,
                        ClaimsPrincipal claims,
                        ISender sender,
                        IChallengePointsConcurrencyGuard guard
                    ) =>
                    {
                        if (!await guard.TryEnterAsync())
                            return Results.StatusCode(StatusCodes.Status429TooManyRequests);

                        try
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
                        finally
                        {
                            guard.Exit();
                        }
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .WithTags(nameof(Hints));
        }
    }
}
