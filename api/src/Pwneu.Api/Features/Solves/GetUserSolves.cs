using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
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
            {
                var managerRoleIds = await context
                    .Roles
                    .Where(r =>
                        r.Name != null &&
                        (r.Name.Equals(Constants.Manager) ||
                         r.Name.Equals(Constants.Admin)))
                    .Select(r => r.Id)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                return await context
                    .UserRoles
                    .Where(ur => managerRoleIds.Contains(ur.RoleId))
                    .Select(ur => ur.UserId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            }, token: cancellationToken);

            if (managerIds.Contains(request.Id.ToString()))
                return Result.Failure<PagedList<UserSolveResponse>>(NotFound);

            var userSolvesRequest = context
                .Solves
                .Where(s => s.UserId == request.Id.ToString())
                .OrderByDescending(s => s.SolvedAt)
                .Select(s => new UserSolveResponse(s.ChallengeId, s.Challenge.Name, s.SolvedAt));

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
                .RequireAuthorization(Constants.ManagerAdminOnly)
                .WithTags(nameof(Solves));
        }
    }
}