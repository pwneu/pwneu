using MediatR;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.PlayConfigurations;

public static class SetPublicLeaderboardCount
{
    public record Command(int Count) : IRequest<Result>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request,
            CancellationToken cancellationToken)
        {
            await context.SetPlayConfigurationValueAsync(
                Consts.PublicLeaderboardCount,
                request.Count,
                cancellationToken);

            await cache.RemoveAsync(Keys.PublicLeaderboardCount(), token: cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("configurations/publicLeaderboardCount", async (int count, ISender sender) =>
                {
                    var query = new Command(count);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest() : Results.NoContent();
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(PlayConfigurations));
        }
    }
}