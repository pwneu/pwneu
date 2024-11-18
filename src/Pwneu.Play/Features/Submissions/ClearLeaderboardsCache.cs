using MediatR;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Submissions;

public static class ClearLeaderboardsCache
{
    public record Command : IRequest<Result>;

    internal sealed class Handler(IFusionCache cache) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            await cache.RemoveAsync(Keys.UserRanks(), token: cancellationToken);
            await cache.RemoveAsync(Keys.TopUsersGraph(), token: cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("leaderboards/clear", async (ISender sender) =>
                {
                    var query = new Command();
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(Submissions));
        }
    }
}