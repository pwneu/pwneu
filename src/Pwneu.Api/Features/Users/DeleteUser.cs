using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Users;

public static class DeleteUser
{
    public record Command(string Id, string RequesterId) : IRequest<Result>;

    private static readonly Error NotFound = new(
        "DeleteUser.NotFound",
        "The user with the specified ID was not found"
    );

    private static readonly Error Failed = new("DeleteUser.Failed", "Failed to delete user");

    private static readonly Error CannotSelfDelete = new(
        "DeleteUser.CannotSelfDelete",
        "Cannot delete yourself"
    );

    private static readonly Error CannotDeleteAdmin = new(
        "DeleteUser.CannotDeleteAdmin",
        "Admin cannot be deleted"
    );

    internal sealed class Handler(UserManager<User> userManager, IFusionCache cache)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByIdAsync(request.Id);

            if (user is null)
                return Result.Failure(NotFound);

            // Even though only admins are allowed to delete a user, still double check.
            if (user.Id == request.RequesterId)
                return Result.Failure(CannotSelfDelete);

            var userIsAdmin = await userManager.IsInRoleAsync(user, Roles.Admin);

            // Admin shouldn't be deleted.
            if (userIsAdmin)
                return Result.Failure(CannotDeleteAdmin);

            var deleteUser = await userManager.DeleteAsync(user);

            if (!deleteUser.Succeeded)
                return Result.Failure(Failed);

            var invalidationTasks = new List<Task>
            {
                cache
                    .RemoveAsync(CacheKeys.UserToken(request.Id), token: cancellationToken)
                    .AsTask(),
                cache
                    .RemoveAsync(CacheKeys.UserDetailsNoEmail(request.Id), token: cancellationToken)
                    .AsTask(),
                cache
                    .RemoveAsync(CacheKeys.UserExists(request.Id), token: cancellationToken)
                    .AsTask(),
                cache.RemoveAsync(CacheKeys.UserRanks(), token: cancellationToken).AsTask(),
                cache.RemoveAsync(CacheKeys.TopUsersGraph(), token: cancellationToken).AsTask(),
            };

            await Task.WhenAll(invalidationTasks);

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete(
                    "identity/users/{id}",
                    async (string id, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var requesterId = claims.GetLoggedInUserId<string>();
                        if (requesterId is null)
                            return Results.BadRequest();

                        var query = new Command(id, requesterId);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.NoContent();
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .WithTags(nameof(Users));
        }
    }
}
