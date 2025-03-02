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

public static class GetUserGainedPoints
{
    private static readonly Error NotFound = new(
        "GetUserGainedPoints.NotFound",
        "The user with the specified ID was not found"
    );

    public record Query(
        string Id,
        string? SearchTerm = null,
        string? SortBy = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null
    ) : IRequest<Result<PagedList<UserSolveResponse>>>;

    internal sealed class Handler(AppDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<PagedList<UserSolveResponse>>>
    {
        public async Task<Result<PagedList<UserSolveResponse>>> Handle(
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
                return Result.Failure<PagedList<UserSolveResponse>>(NotFound);

            var gainedPointsQuery = context.PointsActivities.Where(pa =>
                pa.UserId == request.Id && pa.IsSolve == true
            );

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                gainedPointsQuery = gainedPointsQuery.Where(s =>
                    s.ChallengeName.Contains(request.SearchTerm)
                    || s.ChallengeId.ToString().Contains(request.SearchTerm)
                );

            Expression<Func<PointsActivity, object>> keySelector = request.SortBy?.ToLower() switch
            {
                "name" or "challengename" => pointsActivity => pointsActivity.ChallengeName,
                _ => pointsActivity => pointsActivity.OccurredAt,
            };

            gainedPointsQuery =
                request.SortOrder?.ToLower() == "desc"
                    ? gainedPointsQuery.OrderByDescending(keySelector)
                    : gainedPointsQuery.OrderBy(keySelector);

            var gainedPointsResponse = gainedPointsQuery.Select(pa => new UserSolveResponse
            {
                ChallengeId = pa.ChallengeId,
                ChallengeName = pa.ChallengeName,
                Points = pa.PointsChange,
                SolvedAt = pa.OccurredAt,
            });

            var userSolves = await PagedList<UserSolveResponse>.CreateAsync(
                gainedPointsResponse,
                request.Page ?? 1,
                Math.Min(request.PageSize ?? 10, 30)
            );

            return userSolves;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/users/{id:Guid}/solves",
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
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .RequireRateLimiting(RateLimitingPolicies.Fixed)
                .WithTags(nameof(Solves));

            app.MapGet(
                    "play/me/solves",
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
