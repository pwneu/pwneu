using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.Users;

/// <summary>
/// Confirms a user email, allowing the user to log in.
/// Only the admin can access this endpoint.
/// </summary>
public static class VerifyUser
{
    public record Command(string Id, string RequesterId) : IRequest<Result>;

    private static readonly Error NotFound = new(
        "VerifyUser.NotFound",
        "The user with the specified ID was not found");

    private static readonly Error Failed = new("VerifyUser.Failed", "Unable to verify user email");

    // This won't happen but check just in case.
    private static readonly Error CannotSelfVerify = new(
        "VerifyUser.CannotSelfVerify",
        "Cannot verify yourself");

    private static readonly Error EmailAlreadyConfirmed = new(
        "VerifyUser.EmailAlreadyConfirmed",
        "Email is already confirmed.");

    internal sealed class Handler(UserManager<User> userManager, IFusionCache cache) : IRequestHandler<Command, Result>
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
                cache.RemoveAsync(Keys.User(request.Id), token: cancellationToken).AsTask(),
                cache.RemoveAsync(Keys.UserDetails(request.Id), token: cancellationToken).AsTask(),
            };

            await Task.WhenAll(invalidationTasks);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("users/{id}/verify", async (string id, ClaimsPrincipal claims, ISender sender) =>
                {
                    var requesterId = claims.GetLoggedInUserId<string>();
                    if (requesterId is null)
                        return Results.BadRequest();

                    var query = new Command(id, requesterId);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(Users));
        }
    }
}