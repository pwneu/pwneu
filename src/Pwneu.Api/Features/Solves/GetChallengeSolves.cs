using MediatR;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Pwneu.Api.Features.Solves;

public static class GetChallengeSolves
{
    public record Query(
        Guid Id,
        bool RequesterIsManager,
        string? SearchTerm = null,
        string? SortBy = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null
    ) : IRequest<Result<PagedList<ChallengeSolveResponse>>>;

    internal sealed class Handler(AppDbContext context)
        : IRequestHandler<Query, Result<PagedList<ChallengeSolveResponse>>>
    {
        public async Task<Result<PagedList<ChallengeSolveResponse>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var challengeSolvesQuery = context.Solves.Where(s => s.ChallengeId == request.Id);

            // Only managers and admin can see solvers who aren't visible on leaderboards.
            if (!request.RequesterIsManager)
                challengeSolvesQuery = challengeSolvesQuery.Where(s => s.User.IsVisibleOnLeaderboards);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                challengeSolvesQuery = challengeSolvesQuery.Where(s =>
                    (s.User.UserName != null && s.User.UserName.Contains(request.SearchTerm))
                    || s.UserId.Contains(request.SearchTerm)
                );

            Expression<Func<Solve, object>> keySelector = request.SortBy?.ToLower() switch
            {
                "name" => solve => solve.User.UserName ?? CommonConstants.Unknown,
                "username" => solve => solve.User.UserName ?? CommonConstants.Unknown,
                _ => solve => solve.SolvedAt,
            };

            challengeSolvesQuery =
                request.SortOrder?.ToLower() == "desc"
                    ? challengeSolvesQuery.OrderByDescending(keySelector)
                    : challengeSolvesQuery.OrderBy(keySelector);

            var challengeSolvesResponse = challengeSolvesQuery.Select(
                s => new ChallengeSolveResponse
                {
                    UserId = s.UserId,
                    UserName = s.User.UserName,
                    SolvedAt = s.SolvedAt,
                }
            );

            var challengeSolves = await PagedList<ChallengeSolveResponse>.CreateAsync(
                challengeSolvesResponse,
                request.Page ?? 1,
                Math.Min(request.PageSize ?? 10, 30)
            );

            return challengeSolves;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/challenges/{id:Guid}/solves",
                    async (
                        Guid id,
                        string? searchTerm,
                        string? sortBy,
                        string? sortOrder,
                        int? page,
                        int? pageSize,
                        ClaimsPrincipal claims,
                        ISender sender
                    ) =>
                    {
                        var requesterIsManager = claims.IsInRole(Roles.Manager);

                        var query = new Query(
                            id,
                            requesterIsManager,
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
                .WithTags(nameof(Solves));
        }
    }
}
