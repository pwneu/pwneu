using MediatR;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Extensions.Entities;
using System.Linq.Expressions;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.HintUsages;

public static class GetUserHintUsages
{
    public record Query(
        string Id,
        string? SearchTerm = null,
        string? SortBy = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null
    ) : IRequest<Result<PagedList<UserHintUsageResponse>>>;

    private static readonly Error NotFound = new(
        "GetUserHintUsages.NotFound",
        "The user with the specified ID was not found"
    );

    internal sealed class Handler(AppDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<PagedList<UserHintUsageResponse>>>
    {
        public async Task<Result<PagedList<UserHintUsageResponse>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var userExists = await cache.CheckIfUserExistsAsync(
                context,
                request.Id,
                cancellationToken
            );

            if (!userExists)
                return Result.Failure<PagedList<UserHintUsageResponse>>(NotFound);

            var hintUsagesQuery = context.HintUsages.Where(hu => hu.UserId == request.Id);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                hintUsagesQuery = hintUsagesQuery.Where(hu =>
                    hu.Hint.Challenge.Name.Contains(request.SearchTerm)
                    || hu.Hint.ChallengeId.ToString().Contains(request.SearchTerm)
                    || hu.HintId.ToString().Contains(request.SearchTerm)
                );

            Expression<Func<HintUsage, object>> keySelector = request.SortBy?.ToLower() switch
            {
                "name" or "challengename" => hintUsage => hintUsage.Hint.Challenge.Name,
                "deduction" => hintUsage => hintUsage.Hint.Deduction,
                _ => hintUsage => hintUsage.UsedAt,
            };

            hintUsagesQuery =
                request.SortOrder?.ToLower() == "desc"
                    ? hintUsagesQuery.OrderByDescending(keySelector)
                    : hintUsagesQuery.OrderBy(keySelector);

            var hintUsageResponses = hintUsagesQuery.Select(hu => new UserHintUsageResponse
            {
                HintId = hu.HintId,
                ChallengeId = hu.Hint.ChallengeId,
                ChallengeName = hu.Hint.Challenge.Name,
                UsedAt = hu.UsedAt,
                Deduction = hu.Hint.Deduction,
            });

            var hintUsages = await PagedList<UserHintUsageResponse>.CreateAsync(
                hintUsageResponses,
                request.Page ?? 1,
                Math.Min(request.PageSize ?? 10, 20)
            );

            return hintUsages;
        }
    }

    // Endpoint disabled.
    public class Endpoint // : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/users/{id:Guid}/hintUsages",
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
                        var query = new Query(
                            id.ToString(),
                            searchTerm,
                            sortBy,
                            sortOrder,
                            page,
                            pageSize
                        );
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.NotFound(result.Error)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization()
                .RequireRateLimiting(RateLimitingPolicies.Fixed)
                .WithTags(nameof(HintUsages));

            app.MapGet(
                    "play/me/hintUsages",
                    async (
                        string? searchTerm,
                        string? sortBy,
                        string? sortOrder,
                        int? page,
                        int? pageSize,
                        ClaimsPrincipal claims,
                        ISender sender
                    ) =>
                    {
                        var id = claims.GetLoggedInUserId<string>();
                        if (id is null)
                            return Results.BadRequest();

                        var query = new Query(id, searchTerm, sortBy, sortOrder, page, pageSize);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.NotFound(result.Error)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.MemberOnly)
                .RequireRateLimiting(RateLimitingPolicies.Fixed)
                .WithTags(nameof(HintUsages));
        }
    }
}
