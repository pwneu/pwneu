using MediatR;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.Features.Solves;

public static class GetUserSolves
{
    public record Query(string Id, int? Page = null, int? PageSize = null)
        : IRequest<Result<PagedList<UserSolveResponse>>>;

    internal sealed class Handler(
        ApplicationDbContext context)
        : IRequestHandler<Query, Result<PagedList<UserSolveResponse>>>
    {
        public async Task<Result<PagedList<UserSolveResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var userSolvesRequest = context
                .Submissions
                .Where(s => s.UserId == request.Id && s.IsCorrect == true)
                .OrderByDescending(s => s.SubmittedAt)
                .Select(s => new UserSolveResponse
                {
                    ChallengeId = s.ChallengeId,
                    ChallengeName = s.Challenge.Name,
                    SolvedAt = s.SubmittedAt
                });

            var userSolves = await PagedList<UserSolveResponse>.CreateAsync(userSolvesRequest, request.Page ?? 1,
                request.PageSize ?? 10);

            return userSolves;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/{id:Guid}/solves",
                    async (Guid id, int? page, int? pageSize, ISender sender) =>
                    {
                        var query = new Query(id.ToString(), page, pageSize);
                        var result = await sender.Send(query);

                        return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                    })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Solves));
        }
    }
}