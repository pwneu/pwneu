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

namespace Pwneu.Api.Features.PointsActivities;

public static class GetUserLostPoints
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
        "GetUserLostPoints.NotFound",
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

            var lostPointsQuery = context.PointsActivities.Where(pa =>
                pa.UserId == request.Id && pa.IsSolve == false
            );

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                lostPointsQuery = lostPointsQuery.Where(pa =>
                    pa.ChallengeName.Contains(request.SearchTerm)
                    || pa.ChallengeId.ToString().Contains(request.SearchTerm)
                    || pa.HintId.ToString().Contains(request.SearchTerm)
                );

            Expression<Func<PointsActivity, object>> keySelector = request.SortBy?.ToLower() switch
            {
                "name" or "challengename" => pointsActivity => pointsActivity.ChallengeName,
                "deduction" => pointsActivity => pointsActivity.PointsChange,
                _ => pointsActivity => pointsActivity.OccurredAt,
            };

            lostPointsQuery =
                request.SortOrder?.ToLower() == "desc"
                    ? lostPointsQuery.OrderByDescending(keySelector)
                    : lostPointsQuery.OrderBy(keySelector);

            var lostPointsResponse = lostPointsQuery.Select(pa => new UserHintUsageResponse
            {
                HintId = pa.HintId,
                ChallengeId = pa.ChallengeId,
                ChallengeName = pa.ChallengeName,
                UsedAt = pa.OccurredAt,
                Deduction = pa.PointsChange,
            });

            var lostPoints = await PagedList<UserHintUsageResponse>.CreateAsync(
                lostPointsResponse,
                request.Page ?? 1,
                Math.Min(request.PageSize ?? 10, 20)
            );

            return lostPoints;
        }
    }

    public class Endpoint : IV1Endpoint
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
                .WithTags(nameof(PointsActivities));

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
                .WithTags(nameof(PointsActivities));
        }
    }
}
