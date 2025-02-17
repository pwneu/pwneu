using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Hints;

/// <summary>
/// Check if the user has used the hint.
/// Returns true or false.
/// </summary>
public static class CheckIfHintUsed
{
    public record Command(string UserId, Guid HintId) : IRequest<Result<bool>>;

    private static readonly Error NotFound = new("CheckIfHintUsed.NotFound",
        "The hint with the specified ID was not found");

    private static readonly Error ChallengeAlreadySolved = new(
        "CheckIfHintUsed.ChallengeAlreadySolved",
        "Challenge already solved");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Command, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Command request, CancellationToken cancellationToken)
        {
            // Get hint details first.
            var hintDetails = await context
                .Hints
                .Where(h => h.Id == request.HintId)
                .Select(h => new
                {
                    h.ChallengeId,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (hintDetails is null)
                return Result.Failure<bool>(NotFound);

            // Check if the user has already solved the challenge.
            var hasSolved = await cache.GetOrSetAsync(
                Keys.HasSolved(request.UserId, hintDetails.ChallengeId),
                async _ => await context
                    .Solves
                    .AnyAsync(s =>
                        s.UserId == request.UserId &&
                        s.ChallengeId == hintDetails.ChallengeId, cancellationToken), token: cancellationToken);

            if (hasSolved)
                return Result.Failure<bool>(ChallengeAlreadySolved);

            // Check if the user has used the hint.
            var hintUsed = await context
                .HintUsages
                .AnyAsync(hu => hu.UserId == request.UserId &&
                                hu.HintId == request.HintId,
                    cancellationToken);

            return hintUsed;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("hints/{id:Guid}/check", async (Guid id, ClaimsPrincipal claims, ISender sender) =>
                {
                    var userId = claims.GetLoggedInUserId<string>();
                    if (userId is null) return Results.BadRequest();

                    var query = new Command(userId, id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.MemberOnly)
                .RequireRateLimiting(Consts.Fixed)
                .WithTags(nameof(Hints));
        }
    }
}