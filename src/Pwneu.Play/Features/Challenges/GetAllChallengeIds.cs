using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Challenges;

/// <summary>
/// Queries all challenge ids.
/// </summary>
public static class GetAllChallengeIds
{
    public record Query : IRequest<Result<List<Guid>>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<List<Guid>>>
    {
        public async Task<Result<List<Guid>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var challengeIds = await cache.GetOrSetAsync(Keys.ChallengeIds(), async _ =>
                    await context
                        .Challenges
                        .Select(ch => ch.Id)
                        .ToListAsync(cancellationToken),
                new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(20) },
                cancellationToken);

            return challengeIds;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("challenges/all", async (ISender sender) =>
                {
                    var query = new Query();
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.StatusCode(500) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .WithTags(nameof(Challenges));
        }
    }
}