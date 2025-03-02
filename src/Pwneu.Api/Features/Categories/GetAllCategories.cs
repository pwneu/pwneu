using MediatR;
using Pwneu.Api.Common;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Categories;

public static class GetAllCategories
{
    public record Query : IRequest<Result<IEnumerable<CategoryResponse>>>;

    internal sealed class Handler(AppDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<IEnumerable<CategoryResponse>>>
    {
        public async Task<Result<IEnumerable<CategoryResponse>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var categories = await cache.GetCategoriesAsync(context, cancellationToken);

            return categories;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/categories/all",
                    async (ISender sender) =>
                    {
                        var query = new Query();
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.StatusCode(500)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization()
                .WithTags(nameof(Categories));
        }
    }
}
