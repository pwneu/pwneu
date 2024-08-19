using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.Features.Challenges;

// TODO -- Add option to exclude the user's solved challenges.

/// <summary>
/// Retrieves a paginated list of challenges.
/// </summary>
public static class GetChallenges
{
    public record Query(
        string? SearchTerm = null,
        string? SortColumn = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null)
        : IRequest<Result<PagedList<ChallengeResponse>>>;

    internal sealed class Handler(ApplicationDbContext context)
        : IRequestHandler<Query, Result<PagedList<ChallengeResponse>>>
    {
        public async Task<Result<PagedList<ChallengeResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            IQueryable<Challenge> challengesQuery = context.Challenges.Include(c => c.Artifacts);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                challengesQuery = challengesQuery.Where(c =>
                    c.Name.Contains(request.SearchTerm) ||
                    c.Description.Contains(request.SearchTerm));

            Expression<Func<Challenge, object>> keySelector = request.SortColumn?.ToLower() switch
            {
                "description" => challenge => challenge.Description,
                "points" => challenge => challenge.Points,
                "deadline" => challenge => challenge.Deadline,
                _ => challenge => challenge.Name
            };

            // TODO -- Support excluding solved challenges

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
                request.PageSize ?? 10);

            return challenges;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("challenges", async (string? searchTerm, string? sortColumn, string? sortOrder, int? page,
                    int? pageSize, ISender sender) =>
                {
                    var query = new Query(searchTerm, sortColumn, sortOrder, page, pageSize);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.StatusCode(500) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .WithTags(nameof(Challenges));
        }
    }
}