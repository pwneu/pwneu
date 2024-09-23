using System.Linq.Expressions;
using MediatR;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.Features.HintUsages;

public static class GetChallengeHintUsages
{
    public record Query(
        Guid Id,
        string? SearchTerm = null,
        string? SortBy = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null)
        : IRequest<Result<PagedList<ChallengeHintUsageResponse>>>;

    internal sealed class Handler(ApplicationDbContext context)
        : IRequestHandler<Query, Result<PagedList<ChallengeHintUsageResponse>>>
    {
        public async Task<Result<PagedList<ChallengeHintUsageResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var hintUsagesQuery = context
                .HintUsages
                .Where(hu => hu.Hint.ChallengeId == request.Id);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                hintUsagesQuery = hintUsagesQuery.Where(hu => hu.Hint.Challenge.Name
                    .Contains(request.SearchTerm));

            Expression<Func<HintUsage, object>> keySelector = request.SortBy?.ToLower() switch
            {
                "name" => hintUsage => hintUsage.Hint.Challenge.Name,
                "challengename" => hintUsage => hintUsage.Hint.Challenge.Name,
                "deduction" => hintUsage => hintUsage.Hint.Deduction,
                _ => hintUsage => hintUsage.UsedAt
            };

            hintUsagesQuery = request.SortOrder?.ToLower() == "desc"
                ? hintUsagesQuery.OrderByDescending(keySelector)
                : hintUsagesQuery.OrderBy(keySelector);

            var hintUsageResponses = hintUsagesQuery
                .Select(hu => new ChallengeHintUsageResponse
                {
                    HintId = hu.HintId,
                    UserId = hu.UserId,
                    UsedAt = hu.UsedAt,
                    Deduction = hu.Hint.Deduction,
                });

            var hintUsages = await PagedList<ChallengeHintUsageResponse>.CreateAsync(
                hintUsageResponses,
                request.Page ?? 1,
                Math.Min(request.PageSize ?? 10, 20));

            return hintUsages;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("challenges/{id:Guid}/hintUsages", async (Guid id, string? searchTerm, string? sortBy,
                    string? sortOrder, int? page, int? pageSize, ISender sender) =>
                {
                    var query = new Query(id, searchTerm, sortBy, sortOrder, page, pageSize);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .RequireRateLimiting(Consts.Fixed)
                .WithTags(nameof(HintUsages));
        }
    }
}