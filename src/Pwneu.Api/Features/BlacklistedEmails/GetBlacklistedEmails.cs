using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.BlacklistedEmails;

public static class GetBlacklistedEmails
{
    public record Query : IRequest<Result<IEnumerable<BlacklistedEmail>>>;

    internal sealed class Handler(AppDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<IEnumerable<BlacklistedEmail>>>
    {
        public async Task<Result<IEnumerable<BlacklistedEmail>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var blacklistedEmails = await cache.GetOrSetAsync(
                CacheKeys.BlacklistedEmails(),
                async _ => await context.BlacklistedEmails.ToListAsync(cancellationToken),
                token: cancellationToken
            );

            return blacklistedEmails;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "identity/blacklist",
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
                .WithTags(nameof(BlacklistedEmails));
        }
    }
}
