using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.AccessKeys;

public static class GetAccessKeys
{
    public record Query : IRequest<Result<IEnumerable<AccessKeyResponse>>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<IEnumerable<AccessKeyResponse>>>
    {
        public async Task<Result<IEnumerable<AccessKeyResponse>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var accessKeys = await cache.GetOrSetAsync(Keys.AccessKeys(), async _ =>
                await context
                    .AccessKeys
                    .Select(a => new AccessKeyResponse(a.Id, a.Key, a.CanBeReused, a.Expiration))
                    .ToListAsync(cancellationToken), token: cancellationToken);

            return accessKeys;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("keys", async (ISender sender) =>
                {
                    var query = new Query();
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.StatusCode(500) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(Categories));
        }
    }
}