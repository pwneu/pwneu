using MediatR;
using Pwneu.Api.Common;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions;
using Pwneu.Api.Extensions.Entities;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Challenges;

public static class GetChallenge
{
    public record Query(Guid Id, string UserId) : IRequest<Result<ChallengeDetailsNoFlagResponse>>;

    private static readonly Error NotFound = new(
        "GetChallenge.Null",
        "The challenge with the specified ID was not found"
    );

    internal sealed class Handler(AppDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<ChallengeDetailsNoFlagResponse>>
    {
        public async Task<Result<ChallengeDetailsNoFlagResponse>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            // Cache categories first to check if the cached challenge details is still valid.
            var categories = await cache.GetCategoriesAsync(context, cancellationToken);

            // Get the challenge details.
            var challenge = await cache.GetChallengeDetailsByIdAsync(
                context,
                request.Id,
                cancellationToken
            );

            if (challenge is null)
                return Result.Failure<ChallengeDetailsNoFlagResponse>(NotFound);

            // Check if the category id of the challenge exists in the category.
            bool categoryExists = categories.Any(c => c.Id == challenge?.CategoryId);
            if (!categoryExists)
                return Result.Failure<ChallengeDetailsNoFlagResponse>(NotFound);

            // Cache submissions allowed configuration because why not?
            await cache.CheckIfSubmissionsAllowedAsync(context, cancellationToken);

            // Cache if user exists because why not?
            await cache.CheckIfUserExistsAsync(context, request.UserId, cancellationToken);

            return new ChallengeDetailsNoFlagResponse
            {
                Id = challenge.Id,
                CategoryId = challenge.CategoryId,
                CategoryName = challenge.CategoryName,
                Name = challenge.Name,
                Description = challenge.Description,
                Points = challenge.Points,
                DeadlineEnabled = challenge.DeadlineEnabled,
                Deadline = challenge.Deadline,
                MaxAttempts = challenge.MaxAttempts,
                SolveCount = challenge.SolveCount,
                Tags = challenge.Tags,
                Artifacts = challenge.Artifacts,
                Hints = challenge.Hints,
            };
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/challenges/{id:Guid}",
                    async (Guid id, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null)
                            return Results.BadRequest();

                        var query = new Query(id, userId);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.NotFound(result.Error)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization()
                .WithTags(nameof(Challenges));
        }
    }
}
