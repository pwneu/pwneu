using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Entities;
using Pwneu.Api.Options;
using System.Net;

namespace Pwneu.Api.Features.Users;

public static class CreateResetPasswordLink
{
    public record Command(string UserId) : IRequest<Result<string>>;

    private static readonly Error NotFound = new(
        "CreateResetPasswordLink.NotFound",
        "The user with the specified ID was not found"
    );

    private static readonly Error NotVerified = new(
        "CreateResetPasswordLink.NotVerified",
        "Please verify the user first"
    );

    private static readonly Error NotAllowedOnAdmin = new(
        "CreateResetPasswordLink.AdminNotAllowed",
        "It's not allowed to create reset password link on admin"
    );

    internal sealed class Handler(IOptions<AppOptions> appOptions, UserManager<User> userManager)
        : IRequestHandler<Command, Result<string>>
    {
        private readonly AppOptions _appOptions = appOptions.Value;

        public async Task<Result<string>> Handle(
            Command request,
            CancellationToken cancellationToken
        )
        {
            var user = await userManager.FindByIdAsync(request.UserId);

            if (user is null)
                return Result.Failure<string>(NotFound);

            if (!user.EmailConfirmed)
                return Result.Failure<string>(NotVerified);

            var userIsAdmin = await userManager.IsInRoleAsync(user, Roles.Admin);
            if (userIsAdmin)
                return Result.Failure<string>(NotAllowedOnAdmin);

            var token = await userManager.GeneratePasswordResetTokenAsync(user);

            var encodedEmail = WebUtility.UrlEncode(user.Email);
            var encodedToken = WebUtility.UrlEncode(token);

            var url =
                $"{_appOptions.ResetPasswordUrl}?email={encodedEmail}&resetToken={encodedToken}";

            return url;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "identity/users/{userId:Guid}/resetPassword",
                    async (Guid userId, ISender sender) =>
                    {
                        var command = new Command(userId.ToString());

                        var result = await sender.Send(command);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .WithTags(nameof(Users));
        }
    }
}
