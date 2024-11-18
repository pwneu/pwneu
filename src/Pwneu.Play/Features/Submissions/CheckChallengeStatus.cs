using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Submissions;

public static class CheckChallengeStatus
{
    public record Query(string UserId, Guid ChallengeId) : IRequest<Result<ChallengeStatus>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<ChallengeStatus>>
    {
        public async Task<Result<ChallengeStatus>> Handle(Query request, CancellationToken cancellationToken)
        {
            var hasSolved = await cache.GetOrSetAsync(
                Keys.HasSolved(request.UserId, request.ChallengeId),
                async _ => await context
                    .Solves
                    .AnyAsync(s =>
                        s.UserId == request.UserId &&
                        s.ChallengeId == request.ChallengeId, cancellationToken), token: cancellationToken);

            if (hasSolved)
                return ChallengeStatus.AlreadySolved;

            // Check if the submissions are allowed.
            var submissionsAllowed = await cache.GetOrSetAsync(Keys.SubmissionsAllowed(), async _ =>
                    await context.GetPlayConfigurationValueAsync<bool>(Consts.SubmissionsAllowed, cancellationToken),
                token: cancellationToken);

            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (!submissionsAllowed)
                return ChallengeStatus.Disabled;

            return ChallengeStatus.Allowed;
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

                        return result.IsFailure 
                            ? Results.NotFound(result.Error) 
                            : Results.Ok(result.Value.ToString());
                    })
                .RequireAuthorization(Consts.MemberOnly)
                .WithTags(nameof(Submissions));
        }
    }
}