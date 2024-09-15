using System.Security.Claims;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.Auths;

/// <summary>
/// Revoke the refresh token of the current logged user.
/// Must be authorized to access this endpoint.
/// </summary>
public static class Revoke
{
    public record Command(string UserId) : IRequest<Result>;

    private static readonly Error UserNotFound = new("Revoke.UserNotFound", "User not found");
    private static readonly Error UpdateFailed = new("Revoke.UpdateFailed", "Failed to update user");

    internal sealed class Handler(UserManager<User> userManager, IFusionCache cache) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByIdAsync(request.UserId);

            if (user is null)
                return Result.Failure(UserNotFound);

            await cache.RemoveAsync(user.Id, token: cancellationToken);

            user.RefreshToken = null;

            var updateResult = await userManager.UpdateAsync(user);

            return updateResult.Succeeded ? Result.Success() : Result.Failure(UpdateFailed);
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("revoke", async (ClaimsPrincipal claims, HttpContext httpContext, ISender sender) =>
                {
                    var userId = claims.GetLoggedInUserId<string>();
                    if (userId is null) return Results.Unauthorized();

                    var command = new Command(userId);
                    var result = await sender.Send(command);

                    var cookieOptions = new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddDays(-1),
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict
                    };

                    httpContext.Response.Cookies.Append(Consts.RefreshToken, string.Empty, cookieOptions);

                    return result.IsFailure ? Results.Unauthorized() : Results.NoContent();
                })
                .RequireAuthorization()
                .WithTags(nameof(Auths));
        }
    }
}

public class PasswordChangedEventConsumer(ISender sender, ILogger<PasswordChangedEventConsumer> logger)
    : IConsumer<PasswordResetEvent>
{
    public async Task Consume(ConsumeContext<PasswordResetEvent> context)
    {
        try
        {
            logger.LogInformation("Received password changed event message");

            var message = context.Message;
            var command = new Revoke.Command(message.UserId);
            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                logger.LogInformation("Password reset on {userId}", message.UserId);
                return;
            }

            logger.LogError(
                "Failed to send reset password token to {userId}: {error}", message.UserId, result.Error.Message);
        }
        catch (Exception e)
        {
            logger.LogError("{e}", e.Message);
        }
    }
}