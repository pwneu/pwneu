using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Services;
using System.Security.Claims;

namespace Pwneu.Api.Features.Profile;

public static class ChangePassword
{
    public record Command(
        string UserId,
        string CurrentPassword,
        string NewPassword,
        string RepeatPassword
    ) : IRequest<Result>;

    private static readonly Error NotFound = new(
        "ChangePassword.NotFound",
        "The user with the specified ID was not found"
    );

    private static readonly Error Failed = new(
        "ChangePassword.Failed",
        "Unable to change password"
    );

    internal sealed class Handler(
        IPasswordChecker passwordChecker,
        UserManager<User> userManager,
        IValidator<Command> validator,
        ILogger<Handler> logger
    ) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(
                    new Error("ChangePassword.Validation", validationResult.ToString())
                );

            var checkPassword = passwordChecker.IsPasswordAllowed(request.NewPassword);
            if (checkPassword.IsFailure)
                return Result.Failure(checkPassword.Error);

            var user = await userManager.FindByIdAsync(request.UserId);

            if (user is null)
                return Result.Failure(NotFound);

            var updatePassword = await userManager.ChangePasswordAsync(
                user,
                request.CurrentPassword,
                request.NewPassword
            );

            if (!updatePassword.Succeeded)
                return Result.Failure(Failed);

            logger.LogInformation(
                "Password changed: {UserId}, User: {UserName}",
                request.UserId,
                user.UserName
            );

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPut(
                    "identity/me/password",
                    async (
                        string currentPassword,
                        string newPassword,
                        string repeatPassword,
                        ClaimsPrincipal claims,
                        ISender sender
                    ) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null)
                            return Results.BadRequest();

                        var command = new Command(
                            userId,
                            currentPassword,
                            newPassword,
                            repeatPassword
                        );

                        var result = await sender.Send(command);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.NoContent();
                    }
                )
                .RequireAuthorization()
                .RequireRateLimiting(RateLimitingPolicies.OnceEveryMinute)
                .WithTags(nameof(Profile));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.UserId).NotEmpty().WithMessage("User ID is required.");

            RuleFor(c => c.CurrentPassword)
                .NotEmpty()
                .WithMessage("Current password is required.")
                .MinimumLength(12)
                .WithMessage("Current password must be at least 12 characters long.")
                .Matches(@"[A-Z]")
                .WithMessage("Current password must contain at least one uppercase letter.")
                .Matches(@"[a-z]")
                .WithMessage("Current password must contain at least one lowercase letter.")
                .Matches(@"[0-9]")
                .WithMessage("Current password must contain at least one digit.")
                .Matches(@"[\W_]")
                .WithMessage(
                    "Current password must contain at least one non-alphanumeric character."
                );

            RuleFor(c => c.NewPassword)
                .NotEmpty()
                .WithMessage("New password is required.")
                .MinimumLength(12)
                .WithMessage("New password must be at least 12 characters long.")
                .Matches(@"[A-Z]")
                .WithMessage("New password must contain at least one uppercase letter.")
                .Matches(@"[a-z]")
                .WithMessage("New password must contain at least one lowercase letter.")
                .Matches(@"[0-9]")
                .WithMessage("New password must contain at least one digit.")
                .Matches(@"[\W_]")
                .WithMessage("New password must contain at least one non-alphanumeric character.");

            RuleFor(c => c.RepeatPassword)
                .NotEmpty()
                .WithMessage("Repeat password is required.")
                .Equal(c => c.NewPassword)
                .WithMessage("Repeat password must match the new password.");
        }
    }
}
