using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Categories;

/// <summary>
/// Gets all categories sorted by creation date.
/// </summary>
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
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => new CategoryResponse
                    {
                        Id = c.Id,
                        Name = c.Name,
                    })
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