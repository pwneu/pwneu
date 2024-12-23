using System.Linq.Expressions;
using MediatR;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.Features.Submissions;

public static class GetChallengeSolves
{
    public record Query(
        Guid Id,
        string? SearchTerm = null,
        string? SortBy = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null)
        : IRequest<Result<PagedList<ChallengeSolveResponse>>>;

    internal sealed class Handler(ApplicationDbContext context)
        : IRequestHandler<Query, Result<PagedList<ChallengeSolveResponse>>>
    {
        public async Task<Result<PagedList<ChallengeSolveResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var challengeSolvesQuery = context
                .Solves
                .Where(s => s.ChallengeId == request.Id);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                challengeSolvesQuery = challengeSolvesQuery.Where(s =>
                    s.UserName.Contains(request.SearchTerm) ||
                    s.UserId.Contains(request.SearchTerm));

            Expression<Func<Solve, object>> keySelector = request.SortBy?.ToLower() switch
            {
                "name" => solve => solve.UserName,
                "username" => solve => solve.UserName,
                _ => solve => solve.SolvedAt
            };

            challengeSolvesQuery = request.SortOrder?.ToLower() == "desc"
                ? challengeSolvesQuery.OrderByDescending(keySelector)
                : challengeSolvesQuery.OrderBy(keySelector);

            var challengeSolvesResponse = challengeSolvesQuery
                .Select(s => new ChallengeSolveResponse
                {
                    UserId = s.UserId,
                    UserName = s.UserName,
                    SolvedAt = s.SolvedAt
                });

            var challengeSolves = await PagedList<ChallengeSolveResponse>.CreateAsync(
                challengeSolvesResponse,
                request.Page ?? 1,
                Math.Min(request.PageSize ?? 10, 30));

            return challengeSolves;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("challenges/{id:Guid}/solves", async (Guid id, string? searchTerm, string? sortBy,
                    string? sortOrder, int? page, int? pageSize, ISender sender) =>
                {
                    var query = new Query(id, searchTerm, sortBy, sortOrder, page, pageSize);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .RequireRateLimiting(Consts.Fixed)
                .WithTags(nameof(Submissions));
        }
    }
}