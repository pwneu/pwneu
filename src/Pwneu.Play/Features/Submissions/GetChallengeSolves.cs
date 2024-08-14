using MediatR;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.Features.Submissions;

public static class GetChallengeSolves
{
    public record Query(Guid Id, int? Page = null, int? PageSize = null)
        : IRequest<Result<PagedList<ChallengeSolveResponse>>>;

    internal sealed class Handler(ApplicationDbContext context, IMemberAccess memberAccess)
        : IRequestHandler<Query, Result<PagedList<ChallengeSolveResponse>>>
    {
        public async Task<Result<PagedList<ChallengeSolveResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var challengeSolvesRequest = context
                .Submissions
                .Where(s => s.ChallengeId == request.Id && s.IsCorrect == true)
                .OrderByDescending(s => s.SubmittedAt)
                .Select(s => new ChallengeSolveResponse
                {
                    UserId = s.UserId,
                    SolvedAt = s.SubmittedAt
                });

            var challengeSolves = await PagedList<ChallengeSolveResponse>.CreateAsync(
                challengeSolvesRequest,
                request.Page ?? 1,
                request.PageSize ?? 10);

            await Task.WhenAll(challengeSolves.Items.Select(async challengeSolve =>
            {
                challengeSolve.UserName = (await memberAccess.GetMemberAsync(
                    challengeSolve.UserId, cancellationToken))?.UserName;
            }));

            return challengeSolves;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("challenges/{id:Guid}/solves", async (Guid id, int? page, int? pageSize, ISender sender) =>
                {
                    var query = new Query(id, page, pageSize);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .WithTags(nameof(Submissions));
        }
    }
}