using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Users;

/// <summary>
/// Retrieves a user by ID, excluding those with a role of manager or admin.
/// Only users with manager or admin roles can access this endpoint.
/// </summary>
public static class GetUser
{
    public record Query(string Id) : IRequest<Result<UserDetailsResponse>>;

    private static readonly Error NotFound = new("GetUser.NotFound", "User not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<UserDetailsResponse>>
    {
        public async Task<Result<UserDetailsResponse>> Handle(Query request, CancellationToken cancellationToken)
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

            if (managerIds.Contains(request.Id))
                return Result.Failure<UserDetailsResponse>(NotFound);

            // TODO -- Check for bugs in cache invalidations
            var user = await cache.GetOrSetAsync($"{nameof(UserDetailsResponse)}:{request.Id}", async _ =>
            {
                return await context
                    .Users
                    .Where(u => u.Id == request.Id)
                    .Select(u => new UserDetailsResponse(u.Id, u.UserName, u.Email, u.FullName, u.CreatedAt,
                        u.Solves.Sum(s => s.Challenge.Points),
                        u.FlagSubmissions.Count(fs => fs.FlagStatus == FlagStatus.Correct),
                        u.FlagSubmissions.Count(fs => fs.FlagStatus == FlagStatus.Incorrect)))
                    .FirstOrDefaultAsync(cancellationToken);
            }, token: cancellationToken);

            return user ?? Result.Failure<UserDetailsResponse>(NotFound);
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/{id:Guid}", async (Guid id, ISender sender) =>
                {
                    var query = new Query(id.ToString());
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Constants.ManagerAdminOnly)
                .WithTags(nameof(Users));
        }
    }
}