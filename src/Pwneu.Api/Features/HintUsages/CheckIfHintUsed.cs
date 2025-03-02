using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions;
using Pwneu.Api.Extensions.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.HintUsages;

public static class CheckIfHintUsed
{
    public record Command(string UserId, Guid HintId) : IRequest<Result<bool>>;

    private static readonly Error NotFound = new(
        "CheckIfHintUsed.NotFound",
        "The hint with the specified ID was not found"
    );

    private static readonly Error ChallengeAlreadySolved = new(
        "CheckIfHintUsed.ChallengeAlreadySolved",
        "Challenge already solved"
    );

    internal sealed class Handler(AppDbContext context, IFusionCache cache)
        : IRequestHandler<Command, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Command request, CancellationToken cancellationToken)
        {
            // Get hint first.
            var hintDetails = await context
                .Hints.Where(h => h.Id == request.HintId)
                .Select(h => new { h.ChallengeId })
                .FirstOrDefaultAsync(cancellationToken);

            if (hintDetails is null)
                return Result.Failure<bool>(NotFound);

            // Check if the user has already solved the challenge.
            var hasSolved = await cache.CheckIfUserHasSolvedChallengeAsync(
                context,
                request.UserId,
                hintDetails.ChallengeId,
                cancellationToken
            );
            if (hasSolved)
                return Result.Failure<bool>(ChallengeAlreadySolved);

            // Check if the user has used the hint.
            var hintUsed = await cache.CheckIfUserHasUsedHintAsync(
                context,
                request.UserId,
                request.HintId,
                cancellationToken
            );

            return hintUsed;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/hints/{id:Guid}/check",
                    async (Guid id, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null)
                            return Results.BadRequest();

                        var query = new Command(userId, id);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.MemberOnly)
                .RequireRateLimiting(RateLimitingPolicies.Fixed)
                .WithTags(nameof(HintUsages));
        }
    }
}
