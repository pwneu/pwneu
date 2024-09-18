using MediatR;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.PlayConfigurations;

public class AllowSubmissions
{
    public record Query : IRequest<Result>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result>
    {
        public async Task<Result> Handle(Query request,
            CancellationToken cancellationToken)
        {
            await context.SetPlayConfigurationValueAsync(Consts.SubmissionsAllowed, true, cancellationToken);
            await cache.RemoveAsync(Keys.SubmissionsAllowed(), token: cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("configurations/submissionsAllowed/allow", async (ISender sender) =>
                {
                    var query = new Query();
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(PlayConfigurations));
        }
    }
}