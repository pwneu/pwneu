using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.Features.Categories;

/// <summary>
/// Retrieves a paginated list of categories.
/// </summary>
public static class GetCategories
{
    public record Query(
        string? SearchTerm = null,
        string? SortBy = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null) : IRequest<Result<PagedList<CategoryDetailsResponse>>>;

    internal sealed class Handler(ApplicationDbContext context)
        : IRequestHandler<Query, Result<PagedList<CategoryDetailsResponse>>>
    {
        public async Task<Result<PagedList<CategoryDetailsResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            IQueryable<Category> categoriesQuery = context.Categories.Include(ctg => ctg.Challenges);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                categoriesQuery = categoriesQuery.Where(ctg =>
                    ctg.Name.Contains(request.SearchTerm) ||
                    ctg.Description.Contains(request.SearchTerm) ||
                    ctg.Id.ToString().Contains(request.SearchTerm));

            Expression<Func<Category, object>> keySelector = request.SortBy?.ToLower() switch
            {
                "description" => category => category.Description,
                _ => category => category.CreatedAt
            };

            categoriesQuery = request.SortOrder?.ToLower() == "desc"
                ? categoriesQuery.OrderByDescending(keySelector)
                : categoriesQuery.OrderBy(keySelector);

            var categoryResponsesQuery = categoriesQuery
                .Select(ctg => new CategoryDetailsResponse
                {
                    Id = ctg.Id,
                    Name = ctg.Name,
                    Description = ctg.Description,
                    Challenges = ctg.Challenges.Select(ch => new ChallengeResponse
                    {
                        Id = ch.Id,
                        Name = ch.Name,
                        Description = ch.Description,
                        Points = ch.Points,
                        DeadlineEnabled = ch.DeadlineEnabled,
                        Deadline = ch.Deadline,
                        SolveCount = ch.SolveCount
                    }).ToList()
                });

            var categories =
                await PagedList<CategoryDetailsResponse>.CreateAsync(
                    categoryResponsesQuery,
                    request.Page ?? 1,
                    Math.Min(request.PageSize ?? 10, 20));

            return categories;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("categories",
                    async (string? searchTerm, string? sortBy, string? sortOrder, int? page, int? pageSize,
                        ISender sender) =>
                    {
                        var query = new Query(searchTerm, sortBy, sortOrder, page, pageSize);
                        var result = await sender.Send(query);

                        return result.IsFailure ? Results.StatusCode(500) : Results.Ok(result.Value);
                    })
                .RequireAuthorization()
                .RequireRateLimiting(Consts.Fixed)
                .WithTags(nameof(Categories));
        }
    }
}