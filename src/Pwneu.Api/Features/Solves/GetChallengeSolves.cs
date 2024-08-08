using MediatR;
using Pwneu.Api.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Api.Features.Solves;

public static class GetChallengeSolves
{
    public record Query(Guid Id, int? Page = null, int? PageSize = null)
        : IRequest<Result<PagedList<ChallengeSolveResponse>>>;

    internal sealed class Handler(ApplicationDbContext context)
        : IRequestHandler<Query, Result<PagedList<ChallengeSolveResponse>>>
    {
        public async Task<Result<PagedList<ChallengeSolveResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var challengeSolvesRequest = context
                .FlagSubmissions
                .Where(fs => fs.ChallengeId == request.Id && fs.FlagStatus == FlagStatus.Correct)
                .OrderByDescending(fs => fs.SubmittedAt)
                .Select(fs => new ChallengeSolveResponse
                {
                    UserId = fs.UserId,
                    UserName = fs.User.UserName,
                    SolvedAt = fs.SubmittedAt
                });

            var challengeSolves = await PagedList<ChallengeSolveResponse>.CreateAsync(
                challengeSolvesRequest,
                request.Page ?? 1,
                request.PageSize ?? 10);

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
                .WithTags(nameof(Solves));
        }
    }
}