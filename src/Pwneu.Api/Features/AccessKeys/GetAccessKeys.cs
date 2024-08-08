using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.AccessKeys;

public static class GetAccessKeys
{
    public record Query : IRequest<Result<IEnumerable<AccessKeyResponse>>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<IEnumerable<AccessKeyResponse>>>
    {
        public async Task<Result<IEnumerable<AccessKeyResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var accessKeys = await cache.GetOrSetAsync(Keys.AccessKeys(), async _ =>
                await context
                    .AccessKeys
                    .ToListAsync(cancellationToken), token: cancellationToken);

            return accessKeys
                .Select(a =>
                    new AccessKeyResponse
                    {
                        Id = a.Id,
                        Expiration = a.Expiration,
                        CanBeReused = a.CanBeReused
                    })
                .ToList();
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
                .WithTags(nameof(AccessKeys));
        }
    }
}