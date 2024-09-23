using System.Linq.Expressions;
using System.Security.Claims;
using MediatR;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;

namespace Pwneu.Play.Features.Submissions;

public static class GetUserSolves
{
    public record Query(
        string Id,
        string? SearchTerm = null,
        string? SortBy = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null) : IRequest<Result<PagedList<UserSolveResponse>>>;

    private static readonly Error NotFound = new("GetUserSolves.NotFound",
        "The user with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IMemberAccess memberAccess)
        : IRequestHandler<Query, Result<PagedList<UserSolveResponse>>>
    {
        public async Task<Result<PagedList<UserSolveResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            // Check if user exists.
            if (!await memberAccess.MemberExistsAsync(request.Id, cancellationToken))
                return Result.Failure<PagedList<UserSolveResponse>>(NotFound);

            var userSolvesQuery = context
                .Submissions
                .Where(s => s.UserId == request.Id && s.IsCorrect == true);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                userSolvesQuery = userSolvesQuery
                    .Where(s => s.Challenge.Name.Contains(request.SearchTerm));

            Expression<Func<Submission, object>> keySelector = request.SortBy?.ToLower() switch
            {
                "name" => submission => submission.Challenge.Name,
                "challengename" => submission => submission.Challenge.Name,
                _ => submission => submission.SubmittedAt
            };

            userSolvesQuery = request.SortOrder?.ToLower() == "desc"
                ? userSolvesQuery.OrderByDescending(keySelector)
                : userSolvesQuery.OrderBy(keySelector);

            var userSolvesResponse = userSolvesQuery
                .Select(s => new UserSolveResponse
                {
                    ChallengeId = s.ChallengeId,
                    ChallengeName = s.Challenge.Name,
                    SolvedAt = s.SubmittedAt
                });

            var userSolves = await PagedList<UserSolveResponse>.CreateAsync(
                userSolvesResponse,
                request.Page ?? 1,
                Math.Min(request.PageSize ?? 10, 20));

            return userSolves;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/{id:Guid}/solves", async (Guid id, string? searchTerm, string? sortBy, string? sortOrder,
                    int? page, int? pageSize, ISender sender) =>
                {
                    var query = new Query(id.ToString(), searchTerm, sortBy, sortOrder, page, pageSize);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Submissions));

            app.MapGet("me/solves", async (string? searchTerm, string? sortBy, string? sortOrder, int? page,
                    int? pageSize, ClaimsPrincipal claims, ISender sender) =>
                {
                    var id = claims.GetLoggedInUserId<string>();
                    if (id is null) return Results.BadRequest();

                    var query = new Query(id, searchTerm, sortBy, sortOrder, page, pageSize);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.MemberOnly)
                .RequireRateLimiting(Consts.Fixed)
                .WithTags(nameof(Submissions));
        }
    }
}