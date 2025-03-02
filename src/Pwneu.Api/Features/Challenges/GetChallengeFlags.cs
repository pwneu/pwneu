using MediatR;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Challenges;

public static class GetChallengeFlags
{
    public record Query(Guid ChallengeId) : IRequest<Result<IEnumerable<string>>>;

    private static readonly Error ChallengeNotFound = new(
        "GetChallengeFlags.ChallengeNotFound",
        "The challenge with the specified ID was not found"
    );

    internal sealed class Handler(AppDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<IEnumerable<string>>>
    {
        public async Task<Result<IEnumerable<string>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var challengeDetails = await cache.GetChallengeDetailsByIdAsync(
                context,
                request.ChallengeId,
                cancellationToken
            );

            return challengeDetails?.Flags
                ?? Result.Failure<IEnumerable<string>>(ChallengeNotFound);
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/challenges/{id:Guid}/flags",
                    async (Guid id, ISender sender) =>
                    {
                        var query = new Query(id);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.NotFound(result.Error)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .WithTags(nameof(Challenges));
        }
    }
}
