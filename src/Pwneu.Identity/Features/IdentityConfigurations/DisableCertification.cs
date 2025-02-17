using MediatR;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Extensions;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.IdentityConfigurations;

public static class DisableCertification
{
    public record Query : IRequest<Result>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, ILogger<Handler> logger)
        : IRequestHandler<Query, Result>
    {
        public async Task<Result> Handle(Query request,
            CancellationToken cancellationToken)
        {
            await context.SetIdentityConfigurationValueAsync(Consts.IsCertificationEnabled, false, cancellationToken);
            await cache.RemoveAsync(Keys.IsCertificationEnabled(), token: cancellationToken);

            logger.LogInformation("Certification disabled");

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("configurations/isCertificationEnabled/disable", async (ISender sender) =>
                {
                    var query = new Query();
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(IdentityConfigurations));
        }
    }
}