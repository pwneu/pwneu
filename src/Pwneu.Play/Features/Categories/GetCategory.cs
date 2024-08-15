using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Categories;

/// <summary>
/// Retrieves a category by ID.
/// </summary>
public static class GetCategory
{
    private static readonly Error NotFound = new("GetCategory.Null",
        "The category with the specified ID was not found");

    public record Query(Guid Id) : IRequest<Result<CategoryResponse>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<CategoryResponse>>
    {
        public async Task<Result<CategoryResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var category = await cache.GetOrSetAsync(Keys.Category(request.Id), async _ =>
                await context
                    .Categories
                    .Where(ctg => ctg.Id == request.Id)
                    .Select(ctg => new CategoryResponse
                        {
                            Id = ctg.Id,
                            Name = ctg.Name,
                            Description = ctg.Description,
                            Challenges = ctg.Challenges.Select(c => new ChallengeResponse
                            {
                                Id = c.Id,
                                Name = c.Name
                            }).ToList()
                        }
                    )
                    .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

            return category ?? Result.Failure<CategoryResponse>(NotFound);
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("categories/{id:Guid}", async (Guid id, ISender sender) =>
                {
                    var query = new Query(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .WithTags(nameof(Categories));
        }
    }
}