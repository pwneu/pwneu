using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Users;

/// <summary>
/// Retrieves a user by ID, excluding those with a role of faculty or admin.
/// Only users with faculty or admin roles can access this endpoint.
/// </summary>
public class GetUser
{
    public record Query(Guid Id) : IRequest<Result<UserResponse>>;

    private static readonly Error NotFound = new("GetUser.NotFound", "User not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<UserResponse>>
    {
        public async Task<Result<UserResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var managerIds = await cache.GetOrSetAsync("managerIds", async _ =>
            {
                var managerRoleIds = await context
                    .Roles
                    .Where(r =>
                        r.Name != null &&
                        (r.Name.Equals(Constants.Roles.Faculty) ||
                         r.Name.Equals(Constants.Roles.Admin)))
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
                return Result.Failure<UserResponse>(NotFound);

            var user = await cache.GetOrSetAsync($"{nameof(User)}:{request.Id}", async _ =>
            {
                return await context
                    .Users
                    .Where(u =>
                        u.Id == request.Id.ToString() &&
                        !string.Equals(u.UserName, Constants.Roles.User))
                    .Select(u => new UserResponse(u.Id, u.UserName))
                    .FirstOrDefaultAsync(cancellationToken);
            }, token: cancellationToken);

            return user ?? Result.Failure<UserResponse>(NotFound);
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/{id:Guid}", async (Guid id, ISender sender) =>
                {
                    var query = new Query(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Policies.FacultyAdminOnly)
                .WithTags(nameof(User));
        }
    }
}