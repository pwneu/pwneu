using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Solves;

public static class GetUserSolves
{
    public record Query(Guid Id, int? Page = null, int? PageSize = null)
        : IRequest<Result<PagedList<UserSolveResponse>>>;

    private static readonly Error NotFound = new("GetUserSolves.NotFound",
        "The user with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<PagedList<UserSolveResponse>>>
    {
        public async Task<Result<PagedList<UserSolveResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var managerIds = await cache.GetOrSetAsync("managerIds", async _ =>
                await context
                    .UserRoles
                    .Where(ur => context.Roles
                        .Where(r =>
                            r.Name != null &&
                            (r.Name.Equals(Consts.Manager) ||
                             r.Name.Equals(Consts.Admin)))
                        .Select(r => r.Id)
                        .Contains(ur.RoleId))
                    .Select(ur => ur.UserId)
                    .Distinct()
                    .ToListAsync(cancellationToken), token: cancellationToken);

            if (managerIds.Contains(request.Id.ToString()))
                return Result.Failure<PagedList<UserSolveResponse>>(NotFound);

            var userSolvesRequest = context
                .Submissions
                .Where(s => s.UserId == request.Id.ToString() && s.IsCorrect == true)
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
                        var query = new Query(id, page, pageSize);
                        var result = await sender.Send(query);

                        return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                    })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Solves));
        }
    }
}