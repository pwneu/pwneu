using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using System.Linq.Expressions;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Challenges;

public static class GetChallenges
{
    public record Query(
        Guid? CategoryId = null,
        string? UserId = null,
        bool? ExcludeSolves = false,
        string? SearchTerm = null,
        string? SortBy = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null
    ) : IRequest<Result<PagedList<ChallengeResponse>>>;

    internal sealed class Handler(AppDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<PagedList<ChallengeResponse>>>
    {
        public async Task<Result<PagedList<ChallengeResponse>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            IQueryable<Challenge> challengesQuery = context.Challenges;

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                challengesQuery = challengesQuery.Where(ch =>
                    ch.Name.Contains(request.SearchTerm)
                    || ch.Description.Contains(request.SearchTerm)
                    || ch.Id.ToString().Contains(request.SearchTerm)
                );

            if (request.CategoryId is not null)
                challengesQuery = challengesQuery.Where(ch => ch.CategoryId == request.CategoryId);

            if (!string.IsNullOrWhiteSpace(request.UserId) && request.ExcludeSolves is true)
            {
                var userSolveIds = await cache.GetOrSetAsync(
                    CacheKeys.UserSolvedChallengeIds(request.UserId), async _ =>
                        await context
                            .Solves
                            .Where(s => s.UserId == request.UserId)
                            .Select(s => s.ChallengeId)
                            .ToListAsync(cancellationToken), token: cancellationToken);

                challengesQuery = challengesQuery.Where(ch => !userSolveIds.Contains(ch.Id));
            }

            Expression<Func<Challenge, object>> keySelector = request.SortBy?.ToLower() switch
            {
                "points" => challenge => challenge.Points,
                "deadline" => challenge => challenge.Deadline,
                "solves" => challenge => challenge.SolveCount,
                "solvecount" => challenge => challenge.SolveCount,
                "name" => challenge => challenge.Name,
                _ => challenge => challenge.CreatedAt,
            };

            challengesQuery =
                request.SortOrder?.ToLower() == "desc"
                    ? challengesQuery.OrderByDescending(keySelector)
                    : challengesQuery.OrderBy(keySelector);

            var challengeResponsesQuery = challengesQuery.Select(ch => new ChallengeResponse
            {
                Id = ch.Id,
                Name = ch.Name,
                Description = ch.Description,
                Points = ch.Points,
                DeadlineEnabled = ch.DeadlineEnabled,
                Deadline = ch.Deadline,
                SolveCount = ch.SolveCount,
            });

            var challenges = await PagedList<ChallengeResponse>.CreateAsync(
                challengeResponsesQuery,
                request.Page ?? 1,
                Math.Min(request.PageSize ?? 10, 20)
            );

            return challenges;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/challenges",
                    async (
                        Guid? categoryId,
                        bool? excludeSolves,
                        string? searchTerm,
                        string? sortBy,
                        string? sortOrder,
                        int? page,
                        int? pageSize,
                        ClaimsPrincipal claims,
                        ISender sender
                    ) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();

                        var query = new Query(
                            CategoryId: categoryId,
                            UserId: userId,
                            ExcludeSolves: excludeSolves ?? false,
                            SearchTerm: searchTerm,
                            SortBy: sortBy,
                            SortOrder: sortOrder,
                            Page: page,
                            PageSize: pageSize
                        );
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.StatusCode(500)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization()
                .RequireRateLimiting(RateLimitingPolicies.GetChallenges)
                .WithTags(nameof(Challenges));
        }
    }
}
