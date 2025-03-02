using MediatR;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using System.Linq.Expressions;

namespace Pwneu.Api.Features.HintUsages;

public static class GetChallengeHintUsages
{
    public record Query(
        Guid Id,
        string? SearchTerm = null,
        string? SortBy = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null
    ) : IRequest<Result<PagedList<ChallengeHintUsageResponse>>>;

    internal sealed class Handler(AppDbContext context)
        : IRequestHandler<Query, Result<PagedList<ChallengeHintUsageResponse>>>
    {
        public async Task<Result<PagedList<ChallengeHintUsageResponse>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var hintUsagesQuery = context.HintUsages.Where(hu => hu.Hint.ChallengeId == request.Id);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                hintUsagesQuery = hintUsagesQuery.Where(hu =>
                    (hu.User.UserName != null && hu.User.UserName.Contains(request.SearchTerm))
                    || hu.UserId.Contains(request.SearchTerm)
                    || hu.HintId.ToString().Contains(request.SearchTerm)
                );

            Expression<Func<HintUsage, object>> keySelector = request.SortBy?.ToLower() switch
            {
                "name" or "username" => hintUsage =>
                    hintUsage.User.UserName ?? CommonConstants.Unknown,
                "deduction" => hintUsage => hintUsage.Hint.Deduction,
                _ => hintUsage => hintUsage.UsedAt,
            };

            hintUsagesQuery =
                request.SortOrder?.ToLower() == "desc"
                    ? hintUsagesQuery.OrderByDescending(keySelector)
                    : hintUsagesQuery.OrderBy(keySelector);

            var hintUsageResponses = hintUsagesQuery.Select(hu => new ChallengeHintUsageResponse
            {
                HintId = hu.HintId,
                UserId = hu.UserId,
                UserName = hu.User.UserName ?? CommonConstants.Unknown,
                UsedAt = hu.UsedAt,
                Deduction = hu.Hint.Deduction,
            });

            var hintUsages = await PagedList<ChallengeHintUsageResponse>.CreateAsync(
                hintUsageResponses,
                request.Page ?? 1,
                Math.Min(request.PageSize ?? 10, 20)
            );

            return hintUsages;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/challenges/{id:Guid}/hintUsages",
                    async (
                        Guid id,
                        string? searchTerm,
                        string? sortBy,
                        string? sortOrder,
                        int? page,
                        int? pageSize,
                        ISender sender
                    ) =>
                    {
                        var query = new Query(id, searchTerm, sortBy, sortOrder, page, pageSize);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.NotFound(result.Error)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .RequireRateLimiting(RateLimitingPolicies.Fixed)
                .WithTags(nameof(HintUsages));
        }
    }
}
