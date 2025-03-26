using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Entities;
using Pwneu.Api.Services;

namespace Pwneu.Api.Features.Profile;

public static class ResetPassword
{
    public record Command(
        string Email,
        string PasswordResetToken,
        string NewPassword,
        string RepeatPassword,
        string? TurnstileToken = null
    ) : IRequest<Result>;

    private static readonly Error Failed = new("ResetPassword.Failed", "Unable to change password");

    private static readonly Error InvalidAntiSpamToken = new(
        "ResetPassword.InvalidAntiSpamToken",
        "Human verification failed. Please refresh the page and try again"
    );

    internal sealed class Handler(
        IPasswordChecker passwordChecker,
        UserManager<User> userManager,
        IMediator mediator,
        ITurnstileValidator turnstileValidator,
        IValidator<Command> validator
    ) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(
                    new Error("ResetPassword.Validation", validationResult.ToString())
                );

            var checkPassword = passwordChecker.IsPasswordAllowed(request.NewPassword);
            if (checkPassword.IsFailure)
                return Result.Failure(checkPassword.Error);

            // Validate Turnstile from Cloudflare.
            var isValidTurnstile = await turnstileValidator.IsValidTurnstileTokenAsync(
                request.TurnstileToken,
                cancellationToken
            );

            if (!isValidTurnstile)
                return Result.Failure(InvalidAntiSpamToken);

            var user = await userManager.FindByEmailAsync(request.Email);

            // Don't give the requester a clue if the user exists with the specified email.
            if (user is null)
                return Result.Failure(Failed);

            var resetPassword = await userManager.ResetPasswordAsync(
                user,
                request.PasswordResetToken,
                request.NewPassword
            );

            if (!resetPassword.Succeeded)
                return Result.Failure(Failed);

            // For logging out all accounts
            await mediator.Publish(new PasswordResetEvent { UserId = user.Id }, cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPut(
                    "identity/resetPassword",
                    async (PasswordResetRequest request, ISender sender) =>
                    {
                        var command = new Command(
                            Email: request.Email,
                            PasswordResetToken: request.PasswordResetToken,
                            NewPassword: request.NewPassword,
                            RepeatPassword: request.RepeatPassword,
                            TurnstileToken: request.TurnstileToken
                        );

                        var result = await sender.Send(command);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.NoContent();
                    }
                )
                .RequireRateLimiting(RateLimitingPolicies.ResetPassword)
                .WithTags(nameof(Profile));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Email)
                .NotEmpty()
                .WithMessage("Email is required.")
                .EmailAddress()
                .WithMessage("Email must be a valid email address.");

            RuleFor(c => c.PasswordResetToken)
                .NotEmpty()
                .WithMessage("Password Reset Token is required.");

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
