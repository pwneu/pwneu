using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.BlacklistedEmails;

public static class GetBlacklistedEmails
{
    public record Query : IRequest<Result<IEnumerable<BlacklistedEmail>>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<IEnumerable<BlacklistedEmail>>>
    {
        public async Task<Result<IEnumerable<BlacklistedEmail>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var blacklistedEmails = await cache.GetOrSetAsync(Keys.BlacklistedEmails(), async _ =>
                await context
                    .BlacklistedEmails
                    .ToListAsync(cancellationToken), token: cancellationToken);

            return blacklistedEmails;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("blacklist", async (ISender sender) =>
                {
                    var query = new Query();
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.StatusCode(500) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(BlacklistedEmails));
        }
    }
}