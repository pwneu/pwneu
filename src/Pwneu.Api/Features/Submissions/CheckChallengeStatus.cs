using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Submissions;

public static class CheckChallengeStatus
{
    public record Query(string UserId, Guid ChallengeId) : IRequest<Result<ChallengeStatus>>;

    internal sealed class Handler(AppDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<ChallengeStatus>>
    {
        public async Task<Result<ChallengeStatus>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var hasSolved = await cache.GetOrSetAsync(
                CacheKeys.UserHasSolvedChallenge(request.UserId, request.ChallengeId),
                async _ =>
                    await context.Solves.AnyAsync(
                        s => s.UserId == request.UserId && s.ChallengeId == request.ChallengeId,
                        cancellationToken
                    ),
                token: cancellationToken
            );

            if (hasSolved)
                return ChallengeStatus.AlreadySolved;

            // Check if the submissions are allowed.
            var submissionsAllowed = await cache.GetOrSetAsync(
                CacheKeys.SubmissionsAllowed(),
                async _ =>
                    await context.GetConfigurationValueAsync<bool>(
                        ConfigurationKeys.SubmissionsAllowed,
                        cancellationToken
                    ),
                token: cancellationToken
            );

            if (!submissionsAllowed)
                return ChallengeStatus.Disabled;

            return ChallengeStatus.Allowed;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/challenges/{challengeId:Guid}/check",
                    async (Guid challengeId, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null)
                            return Results.BadRequest();

                        var query = new Query(userId, challengeId);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.NotFound(result.Error)
                            : Results.Ok(result.Value.ToString());
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.MemberOnly)
                .WithTags(nameof(Submissions));
        }
    }
}
