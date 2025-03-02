using MediatR;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.PointsActivities;

public static class ClearLeaderboardsCache
{
    public record Command : IRequest<Result>;

    internal sealed class Handler(IFusionCache cache) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            await cache.RemoveAsync(CacheKeys.UserRanks(), token: cancellationToken);
            await cache.RemoveAsync(CacheKeys.TopUsersGraph(), token: cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "play/leaderboards/clear",
                    async (ISender sender) =>
                    {
                        var query = new Command();
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.NoContent();
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .WithTags(nameof(PointsActivities));
        }
    }
}
