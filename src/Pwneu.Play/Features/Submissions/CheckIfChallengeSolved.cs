using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Submissions;

public static class CheckIfChallengeSolved
{
    public record Query(string UserId, Guid ChallengeId) : IRequest<Result<bool>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Query request, CancellationToken cancellationToken)
        {
            var hasSolved = await cache.GetOrSetAsync(
                Keys.HasSolved(request.UserId, request.ChallengeId),
                async _ => await context
                    .Solves
                    .AnyAsync(s =>
                        s.UserId == request.UserId &&
                        s.ChallengeId == request.ChallengeId, cancellationToken), token: cancellationToken);

            return hasSolved;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("challenges/{challengeId:Guid}/check",
                    async (Guid challengeId, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null) return Results.BadRequest();

                        var query = new Query(userId, challengeId);
                        var result = await sender.Send(query);

                        return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                    })
                .RequireAuthorization(Consts.MemberOnly)
                .WithTags(nameof(Submissions));
        }
    }
}