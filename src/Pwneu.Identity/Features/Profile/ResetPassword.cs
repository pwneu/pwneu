using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Identity.Features.Profile;

public static class ResetPassword
{
    public record Command(string Email, string PasswordResetToken, string NewPassword, string RepeatPassword)
        : IRequest<Result>;

    private static readonly Error Failed = new(
        "ResetPassword.Failed",
        "Unable to change password");

    internal sealed class Handler(
        UserManager<User> userManager,
        IPublishEndpoint publishEndpoint,
        IValidator<Command> validator) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(new Error("ResetPassword.Validation", validationResult.ToString()));

            var user = await userManager.FindByEmailAsync(request.Email);

            // Don't give the requester a clue if the user exists with the specified email.
            if (user is null)
                return Result.Failure(Failed);

            var resetPassword = await userManager.ResetPasswordAsync(
                user,
                request.PasswordResetToken,
                request.NewPassword);

            if (!resetPassword.Succeeded)
                return Result.Failure(Failed);

            // For logging out all accounts
            await publishEndpoint.Publish(new PasswordResetEvent { UserId = user.Id }, cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("resetPassword", async (PasswordResetRequest request, ISender sender) =>
                {
                    var command = new Command(
                        Email: request.Email,
                        PasswordResetToken: request.PasswordResetToken,
                        NewPassword: request.NewPassword,
                        RepeatPassword: request.RepeatPassword);

                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireRateLimiting(Consts.ResetPassword)
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