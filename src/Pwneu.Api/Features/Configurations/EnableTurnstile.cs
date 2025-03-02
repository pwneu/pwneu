using MediatR;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Configurations;

public static class EnableTurnstile
{
    public record Query : IRequest<Result>;

    internal sealed class Handler(AppDbContext context, IFusionCache cache, ILogger<Handler> logger)
        : IRequestHandler<Query, Result>
    {
        public async Task<Result> Handle(Query request, CancellationToken cancellationToken)
        {
            await context.SetConfigurationValueAsync(
                ConfigurationKeys.IsTurnstileEnabled,
                true,
                cancellationToken
            );
            await cache.RemoveAsync(CacheKeys.IsTurnstileEnabled(), token: cancellationToken);

            logger.LogInformation("Turnstile enabled");

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPut(
                    "identity/configurations/isTurnstileEnabled/enable",
                    async (ISender sender) =>
                    {
                        var query = new Query();
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.NoContent();
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .WithTags(nameof(Configurations));
        }
    }
}
