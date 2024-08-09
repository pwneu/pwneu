using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Api.Features.Categories;

/// <summary>
/// Retrieves a paginated list of categories.
/// </summary>
public static class GetCategories
{
    public record Query(
        string? SearchTerm = null,
        string? SortColumn = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null) : IRequest<Result<PagedList<CategoryResponse>>>;

    internal sealed class Handler(ApplicationDbContext context)
        : IRequestHandler<Query, Result<PagedList<CategoryResponse>>>
    {
        public async Task<Result<PagedList<CategoryResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            IQueryable<Category> categoriesQuery = context.Categories.Include(ctg => ctg.Challenges);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                categoriesQuery = categoriesQuery.Where(ctg =>
                    ctg.Name.Contains(request.SearchTerm) ||
                    ctg.Description.Contains(request.SearchTerm));

            Expression<Func<Category, object>> keySelector = request.SortColumn?.ToLower() switch
            {
                "description" => category => category.Description,
                _ => category => category.Name
            };

            categoriesQuery = request.SortOrder?.ToLower() == "desc"
                ? categoriesQuery.OrderByDescending(keySelector)
                : categoriesQuery.OrderBy(keySelector);

            var categoryResponsesQuery = categoriesQuery
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
                });

            var categories =
                await PagedList<CategoryResponse>.CreateAsync(categoryResponsesQuery, request.Page ?? 1,
                    request.PageSize ?? 10);

            return categories;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("categories",
                    async (string? searchTerm, string? sortColumn, string? sortOrder, int? page, int? pageSize,
                        ISender sender) =>
                    {
                        var query = new Query(searchTerm, sortColumn, sortOrder, page, pageSize);
                        var result = await sender.Send(query);

                        return result.IsFailure ? Results.StatusCode(500) : Results.Ok(result.Value);
                    })
                .RequireAuthorization()
                .WithTags(nameof(Categories));
        }
    }
}