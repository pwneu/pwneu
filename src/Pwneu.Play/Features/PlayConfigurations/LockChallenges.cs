using MediatR;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.PlayConfigurations;

public static class LockChallenges
{
    public record Command : IRequest<Result>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, ILogger<Handler> logger)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request,
            CancellationToken cancellationToken)
        {
            await context.SetPlayConfigurationValueAsync(Consts.ChallengesLocked, true, cancellationToken);
            await cache.RemoveAsync(Keys.ChallengesLocked(), token: cancellationToken);

            logger.LogInformation("Challenges locked");

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("configurations/challengesLocked/lock", async (ISender sender) =>
                {
                    var query = new Command();
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(PlayConfigurations));
        }
    }
}