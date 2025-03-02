using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.AccessKeys;

public static class GetAccessKeys
{
    public record Query : IRequest<Result<IEnumerable<AccessKeyResponse>>>;

    internal sealed class Handler(AppDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<IEnumerable<AccessKeyResponse>>>
    {
        public async Task<Result<IEnumerable<AccessKeyResponse>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var accessKeys = await cache.GetOrSetAsync(
                CacheKeys.AccessKeys(),
                async _ => await context.AccessKeys.ToListAsync(cancellationToken),
                token: cancellationToken
            );

            return accessKeys
                .Select(a => new AccessKeyResponse
                {
                    Id = a.Id,
                    ForManager = a.ForManager,
                    Expiration = a.Expiration,
                    CanBeReused = a.CanBeReused,
                })
                .ToList();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "identity/keys",
                    async (ISender sender) =>
                    {
                        var query = new Query();
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.StatusCode(500)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .WithTags(nameof(AccessKeys));
        }
    }
}
