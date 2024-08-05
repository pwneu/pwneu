using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Entities;
using Pwneu.Api.Shared.Extensions;

namespace Pwneu.Api.Features.Auths;

/// <summary>
/// Revoke the refresh token of the current logged user.
/// Must be authorized to access this endpoint.
/// </summary>
public static class Revoke
{
    public record Command(string UserId) : IRequest<Result>;

    internal sealed class Handler(UserManager<User> userManager) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByIdAsync(request.UserId);

            if (user is null)
                return Result.Failure(Error.None);

            user.RefreshToken = null;

            await userManager.UpdateAsync(user);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("revoke", async (ClaimsPrincipal claims, ISender sender) =>
                {
                    var userId = claims.GetLoggedInUserId<string>();
                    if (userId is null) return Results.Unauthorized();

                    var command = new Command(userId);
                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.Unauthorized() : Results.NoContent();
                })
                .RequireAuthorization()
                .WithTags(nameof(Auths));
        }
    }
}