using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Api.Features.Challenges;

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
        : IRequest<Result<PagedList<ChallengeDetailsResponse>>>;

    internal sealed class Handler(ApplicationDbContext context)
        : IRequestHandler<Query, Result<PagedList<ChallengeDetailsResponse>>>
    {
        public async Task<Result<PagedList<ChallengeDetailsResponse>>> Handle(Query request,
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

            challengesQuery = request.SortOrder?.ToLower() == "desc"
                ? challengesQuery.OrderByDescending(keySelector)
                : challengesQuery.OrderBy(keySelector);

            var challengeResponsesQuery = challengesQuery
                .Select(c => new ChallengeDetailsResponse
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    Points = c.Points,
                    DeadlineEnabled = c.DeadlineEnabled,
                    Deadline = c.Deadline,
                    MaxAttempts = c.MaxAttempts,
                    SolveCount = c.FlagSubmissions.Count(fs => fs.FlagStatus == FlagStatus.Correct),
                    Artifacts = c.Artifacts
                        .Select(a => new ArtifactResponse
                        {
                            Id = a.Id,
                            FileName = a.FileName
                        })
                        .ToList()
                });

            var challenges = await PagedList<ChallengeDetailsResponse>.CreateAsync(
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