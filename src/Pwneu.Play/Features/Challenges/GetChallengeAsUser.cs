using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Challenges;

/// <summary>
/// Retrieves a challenge by ID.
/// </summary>
public static class GetChallengeAsUser
{
    public record Query(string UserId, Guid ChallengeId) : IRequest<Result<ChallengeAsUserResponse>>;

    private static readonly Error NotFound = new("GetChallengeAsUser.Null",
        "The challenge with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, IMemberAccess memberAccess)
        : IRequestHandler<Query, Result<ChallengeAsUserResponse>>
    {
        public async Task<Result<ChallengeAsUserResponse>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            // Check if user exists.
            if (!await memberAccess.MemberExistsAsync(request.UserId, cancellationToken))
                return Result.Failure<ChallengeAsUserResponse>(NotFound);

            // Check if the challenge exists.
            var challenge = await cache.GetOrSetAsync(Keys.ChallengeDetails(request.ChallengeId), async _ =>
                await context
                    .Challenges
                    .GetDetailsByIdAsync(
                        request.ChallengeId,
                        cancellationToken), token: cancellationToken);

            if (challenge is null)
                return Result.Failure<ChallengeAsUserResponse>(NotFound);

            var hasSolved = await cache.GetOrSetAsync(
                Keys.HasSolved(request.UserId, request.ChallengeId),
                async _ => await context
                    .Solves
                    .AnyAsync(s =>
                        s.UserId == request.UserId &&
                        s.ChallengeId == request.ChallengeId, cancellationToken), token: cancellationToken);

            int attemptsLeft;
            if (challenge.MaxAttempts > 0)
            {
                // Retrieve the current attempt count from the cache or calculate it if not present.
                var attemptCount = await cache.GetOrSetAsync(
                    Keys.AttemptsLeft(request.UserId, request.ChallengeId),
                    async _ => await context
                        .Submissions
                        .Where(s =>
                            s.UserId == request.UserId &&
                            s.ChallengeId == challenge.Id)
                        .CountAsync(cancellationToken), token: cancellationToken);

                attemptsLeft = challenge.MaxAttempts - attemptCount;
            }
            // If MaxAttempts is not zero, set attemptsLeft to an unlimited value.
            else attemptsLeft = int.MaxValue;

            return new ChallengeAsUserResponse
            {
                Challenge = challenge,
                HasSolved = hasSolved,
                AttemptsLeft = attemptsLeft
            };
        }
    }

    public class Endpoint // : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("challenges/{challengeId:Guid}/me",
                    async (Guid challengeId, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null) return Results.BadRequest();

                        var query = new Query(userId, challengeId);
                        var result = await sender.Send(query);

                        return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                    })
                .RequireAuthorization(Consts.MemberOnly)
                .WithTags(nameof(Challenges));
        }
    }
}