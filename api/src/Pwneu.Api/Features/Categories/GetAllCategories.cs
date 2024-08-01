using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Categories;

public static class GetAllCategories
{
    public record Query : IRequest<Result<IEnumerable<CategoryResponse>>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<IEnumerable<CategoryResponse>>>
    {
        public async Task<Result<IEnumerable<CategoryResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var categories = await cache.GetOrSetAsync(Keys.Categories(), async _ =>
                await context
                    .Categories
                    .Select(c => new CategoryResponse(c.Id, c.Name, c.Description,
                        c.Challenges.Select(ch => new ChallengeResponse(ch.Id, ch.Name))))
                    .ToListAsync(cancellationToken), token: cancellationToken);

            return categories;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("categories/all", async (ISender sender) =>
                {
                    var query = new Query();
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.StatusCode(500) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .WithTags(nameof(Categories));
        }
    }
}