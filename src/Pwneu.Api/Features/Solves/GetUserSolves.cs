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

namespace Pwneu.Api.Features.Solves;

public static class GetUserSolves
{
    private static readonly Error NotFound = new(
        "GetUserSolves.NotFound",
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

            var userSolvesQuery = context.Solves.Where(s => s.UserId == request.Id);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                userSolvesQuery = userSolvesQuery.Where(s =>
                    s.Challenge.Name.Contains(request.SearchTerm)
                    || s.ChallengeId.ToString().Contains(request.SearchTerm)
                );

            Expression<Func<Solve, object>> keySelector = request.SortBy?.ToLower() switch
            {
                "name" or "challengename" => solve => solve.Challenge.Name,
                _ => solve => solve.SolvedAt,
            };

            userSolvesQuery =
                request.SortOrder?.ToLower() == "desc"
                    ? userSolvesQuery.OrderByDescending(keySelector)
                    : userSolvesQuery.OrderBy(keySelector);

            var userSolvesResponse = userSolvesQuery.Select(s => new UserSolveResponse
            {
                ChallengeId = s.ChallengeId,
                ChallengeName = s.Challenge.Name,
                Points = s.Challenge.Points,
                SolvedAt = s.SolvedAt,
            });

            var userSolves = await PagedList<UserSolveResponse>.CreateAsync(
                userSolvesResponse,
                request.Page ?? 1,
                Math.Min(request.PageSize ?? 10, 30)
            );

            return userSolves;
        }
    }

    // Endpoint disabled.
    public class Endpoint // : IV1Endpoint
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
                .WithTags(nameof(Solves));
        }
    }
}
