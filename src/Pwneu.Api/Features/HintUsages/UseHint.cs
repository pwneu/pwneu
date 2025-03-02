using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Extensions.Entities;
using System.Collections.Concurrent;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.HintUsages;

public static class UseHint
{
    public record Command(string UserId, string UserName, Guid HintId) : IRequest<Result<string>>;

    private static readonly Error UserNotFound = new(
        "UseHint.UserNotFound",
        "The user with the specified ID was not found"
    );

    private static readonly Error NotFound = new(
        "UseHint.NotFound",
        "The hint with the specified ID was not found"
    );

    private static readonly Error ChallengeAlreadySolved = new(
        "UseHint.ChallengeAlreadySolved",
        "Challenge already solved"
    );

    private static readonly Error NotAllowed = new(
        "UseHint.NotAllowed",
        "Using hints are not allowed when submissions are disabled"
    );

    internal sealed class Handler(
        AppDbContext context,
        BufferDbContext bufferDbContext,
        IFusionCache cache,
        ILogger<Handler> logger
    ) : IRequestHandler<Command, Result<string>>
    {
        public async Task<Result<string>> Handle(
            Command request,
            CancellationToken cancellationToken
        )
        {
            var userExists = await cache.CheckIfUserExistsAsync(
                context,
                request.UserId,
                cancellationToken
            );

            if (!userExists)
                return Result.Failure<string>(UserNotFound);

            var submissionsAllowed = await cache.CheckIfSubmissionsAllowedAsync(
                context,
                cancellationToken
            );

            if (!submissionsAllowed)
                return Result.Failure<string>(NotAllowed);

            // Get hint details first.
            var hintDetails = await context
                .Hints.Where(h => h.Id == request.HintId)
                .Select(h => new
                {
                    h.Deduction,
                    h.Content,
                    h.ChallengeId,
                    ChallengeName = h.Challenge.Name,
                    h.Challenge.CategoryId,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (hintDetails is null)
                return Result.Failure<string>(NotFound);

            // Check if the user has already solved the challenge.
            var hasSolved = await cache.CheckIfUserHasSolvedChallengeAsync(
                context,
                request.UserId,
                hintDetails.ChallengeId,
                cancellationToken
            );

            if (hasSolved)
                return Result.Failure<string>(ChallengeAlreadySolved);

            // If the user has already used the hint, there's no need to deduct the user's points.
            var alreadyUsedHint = await cache.CheckIfUserHasUsedHintAsync(
                context,
                request.UserId,
                request.HintId,
                cancellationToken
            );

            // The user can get the hint over and over again.
            if (alreadyUsedHint)
                return hintDetails.Content;

            await cache.SetAsync(
                CacheKeys.UserHasUsedHint(request.UserId, request.HintId),
                true,
                token: cancellationToken
            );

            var hintUsageBuffer = HintUsageBuffer.Create(
                request.UserId,
                request.HintId,
                hintDetails.Deduction,
                hintDetails.ChallengeId,
                hintDetails.ChallengeName,
                hintDetails.CategoryId
            );

            bufferDbContext.Add(hintUsageBuffer);

            await bufferDbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Hint used: {HintId}, User: {UserId}",
                request.HintId,
                request.UserId
            );

            return hintDetails.Content;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> UserLocks = new();

        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "play/hints/{hintId:Guid}",
                    async (Guid hintId, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null)
                            return Results.BadRequest();

                        var userName = claims.GetLoggedInUserName();
                        if (userName is null)
                            return Results.BadRequest();

                        // Get or create a semaphore for the userId.
                        var userLock = UserLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));

                        // Try to acquire the lock without waiting.
                        if (!await userLock.WaitAsync(0))
                            return Results.StatusCode(StatusCodes.Status429TooManyRequests);

                        try
                        {
                            // Proceed with the command after acquiring the lock.
                            var command = new Command(userId, userName, hintId);
                            var result = await sender.Send(command);

                            return result.IsFailure
                                ? Results.BadRequest(result.Error)
                                : Results.Ok(result.Value);
                        }
                        finally
                        {
                            userLock.Release();
                        }
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.MemberOnly)
                .RequireRateLimiting(RateLimitingPolicies.UseHint)
                .WithTags(nameof(HintUsages));
        }
    }
}
