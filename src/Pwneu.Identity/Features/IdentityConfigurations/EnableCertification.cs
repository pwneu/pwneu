using MediatR;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Extensions;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.IdentityConfigurations;

public class EnableCertification
{
    public record Query : IRequest<Result>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result>
    {
        public async Task<Result> Handle(Query request,
            CancellationToken cancellationToken)
        {
            await context.SetIdentityConfigurationValueAsync(Consts.IsCertificationEnabled, true, cancellationToken);
            await cache.RemoveAsync(Keys.IsCertificationEnabled(), token: cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("configurations/isCertificationEnabled/enable", async (ISender sender) =>
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