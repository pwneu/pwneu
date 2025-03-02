using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Auths;

public static class Revoke
{
    public record Command(string UserId) : IRequest<Result>;

    private static readonly Error UserNotFound = new("Revoke.UserNotFound", "User not found");
    private static readonly Error RevokeFailed = new(
        "Revoke.RevokeFailed",
        "Failed to revoke token"
    );

    internal sealed class Handler(
        UserManager<User> userManager,
        IFusionCache cache,
        ILogger<Handler> logger
    ) : IRequestHandler<Command, Result>, INotificationHandler<PasswordResetEvent>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByIdAsync(request.UserId);

            if (user is null)
                return Result.Failure(UserNotFound);

            user.RefreshToken = null;

            var updateResult = await userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
                return Result.Failure(RevokeFailed);

            await cache.RemoveAsync(CacheKeys.UserToken(user.Id), token: cancellationToken);

            return Result.Success();
        }

        public async Task Handle(
            PasswordResetEvent notification,
            CancellationToken cancellationToken
        )
        {
            var user = await userManager.FindByIdAsync(notification.UserId);

            if (user is null)
            {
                logger.LogInformation(
                    "Failed to revoke token: User with ID {UserId} not found.",
                    notification.UserId
                );
                return;
            }

            user.RefreshToken = null;

            var updateResult = await userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                logger.LogInformation(
                    "Failed to revoke token for user ID {UserId}.",
                    notification.UserId
                );
                return;
            }

            await cache.RemoveAsync(CacheKeys.UserToken(user.Id), token: cancellationToken);
            logger.LogInformation(
                "Successfully revoked token for user ID {UserId}.",
                notification.UserId
            );
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "identity/revoke",
                    async (ClaimsPrincipal claims, HttpContext httpContext, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null)
                            return Results.Unauthorized();

                        var command = new Command(userId);
                        var result = await sender.Send(command);

                        var cookieOptions = new CookieOptions
                        {
                            Expires = DateTimeOffset.UtcNow.AddDays(-1),
                            HttpOnly = true,
                            Secure = true,
                            SameSite = SameSiteMode.Strict,
                        };

                        httpContext.Response.Cookies.Append(
                            CommonConstants.RefreshToken,
                            string.Empty,
                            cookieOptions
                        );

                        return result.IsFailure ? Results.Unauthorized() : Results.NoContent();
                    }
                )
                .RequireAuthorization()
                .WithTags(nameof(Auths));
        }
    }
}
