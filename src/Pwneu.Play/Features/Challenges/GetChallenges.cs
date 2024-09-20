using System.Linq.Expressions;
using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Challenges;

/// <summary>
/// Retrieves a paginated list of challenges.
/// </summary>
public static class GetChallenges
{
    public record Query(
        Guid? CategoryId = null,
        string? UserId = null,
        bool? ExcludeSolves = false,
        string? SearchTerm = null,
        string? SortColumn = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null) : IRequest<Result<PagedList<ChallengeResponse>>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<PagedList<ChallengeResponse>>>
    {
        public async Task<Result<PagedList<ChallengeResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            IQueryable<Challenge> challengesQuery = context.Challenges;

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                challengesQuery = challengesQuery.Where(ch =>
                    ch.Name.Contains(request.SearchTerm) ||
                    ch.Description.Contains(request.SearchTerm));

            if (request.CategoryId is not null)
                challengesQuery = challengesQuery.Where(ch => ch.CategoryId == request.CategoryId);

            if (!string.IsNullOrWhiteSpace(request.UserId) && request.ExcludeSolves is true)
            {
                var userSolveIds = await cache.GetOrSetAsync(
                    Keys.UserSolveIds(request.UserId), async _ =>
                        await context
                            .Submissions
                            .Where(s => s.UserId == request.UserId && s.IsCorrect)
                            .Select(s => s.ChallengeId)
                            .ToListAsync(cancellationToken), token: cancellationToken);

                challengesQuery = challengesQuery.Where(ch => !userSolveIds.Contains(ch.Id));

                // Use LINQ if the current approach is slower.
                // challengesQuery = challengesQuery.Where(ch =>
                //     !ch.Submissions.Any(s => s.UserId == request.UserId && s.IsCorrect));
            }

            Expression<Func<Challenge, object>> keySelector = request.SortColumn?.ToLower() switch
            {
                "description" => challenge => challenge.Description,
                "points" => challenge => challenge.Points,
                "deadline" => challenge => challenge.Deadline,
                _ => challenge => challenge.Name
            };

            challengesQuery = request.SortOrder?.ToLower() == "desc"
                ? challengesQuery.OrderByDescending(keySelector)
                : challengesQuery.OrderBy(keySelector);

            var challengeResponsesQuery = challengesQuery
                .Select(ch => new ChallengeResponse
                {
                    Id = ch.Id,
                    Name = ch.Name,
                    Description = ch.Description,
                    Points = ch.Points,
                    DeadlineEnabled = ch.DeadlineEnabled,
                    Deadline = ch.Deadline,
                    SolveCount = ch.SolveCount
                });

            var challenges = await PagedList<ChallengeResponse>.CreateAsync(
                challengeResponsesQuery,
                request.Page ?? 1,
                Math.Min(request.PageSize ?? 10, 20));

            return challenges;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("challenges", async (Guid? categoryId, bool? excludeSolves, string? searchTerm,
                    string? sortColumn, string? sortOrder, int? page, int? pageSize, ClaimsPrincipal claims,
                    ISender sender) =>
                {
                    var userId = claims.GetLoggedInUserId<string>();

                    var query = new Query(
                        CategoryId: categoryId,
                        UserId: userId,
                        ExcludeSolves: excludeSolves ?? false,
                        SearchTerm: searchTerm,
                        SortColumn: sortColumn,
                        SortOrder: sortOrder,
                        Page: page,
                        PageSize: pageSize);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.StatusCode(500) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .RequireRateLimiting(Consts.Fixed)
                .WithTags(nameof(Challenges));
        }
    }
}
