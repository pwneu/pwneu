using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Users;

public static class VerifyUser
{
    public record Command(string Id, string RequesterId) : IRequest<Result>;

    private static readonly Error NotFound = new(
        "VerifyUser.NotFound",
        "The user with the specified ID was not found"
    );

    private static readonly Error Failed = new("VerifyUser.Failed", "Unable to verify user email");

    // This won't happen but check just in case.
    private static readonly Error CannotSelfVerify = new(
        "VerifyUser.CannotSelfVerify",
        "Cannot verify yourself"
    );

    private static readonly Error EmailAlreadyConfirmed = new(
        "VerifyUser.EmailAlreadyConfirmed",
        "Email is already confirmed."
    );

    internal sealed class Handler(UserManager<User> userManager, IFusionCache cache)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByIdAsync(request.Id);

            if (user is null)
                return Result.Failure(NotFound);

            if (user.EmailConfirmed)
                return Result.Failure(EmailAlreadyConfirmed);

            // Even though only admins are allowed to verify a user, still double check.
            if (user.Id == request.RequesterId)
                return Result.Failure(CannotSelfVerify);

            var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);

            var verifyEmail = await userManager.ConfirmEmailAsync(user, confirmationToken);

            if (!verifyEmail.Succeeded)
                Result.Failure(Failed);

            var invalidationTasks = new List<Task>
            {
                cache
                    .RemoveAsync(CacheKeys.UserDetailsNoEmail(request.Id), token: cancellationToken)
                    .AsTask(),
            };

            await Task.WhenAll(invalidationTasks);

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPut(
                    "identity/users/{id}/verify",
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
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .WithTags(nameof(Users));
        }
    }
}
