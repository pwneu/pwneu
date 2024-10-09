using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Identity.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Identity.Features.Profile;

/// <summary>
/// Generates a password reset and sent via email.
/// If email verification is disabled, this will also be disabled.
/// </summary>
public static class ForgotPassword
{
    public record Command(string Email, string? TurnstileToken = null) : IRequest<Result>;

    private static readonly Error InvalidAntiSpamToken = new("ForgotPassword.InvalidAntiSpamToken",
        "Invalid turnstileToken token. Rejecting request");

    internal sealed class Handler(
        UserManager<User> userManager,
        ITurnstileValidator turnstileValidator,
        IPublishEndpoint publishEndpoint,
        IValidator<Command> validator) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(new Error("ForgotPassword.Validation", validationResult.ToString()));

            var isValidTurnstileToken = await turnstileValidator.IsValidTurnstileTokenAsync(
                request.TurnstileToken,
                cancellationToken);

            if (!isValidTurnstileToken)
                return Result.Failure(InvalidAntiSpamToken);

            var user = await userManager.FindByEmailAsync(request.Email);

            // Don't give the requester a clue if the user exists with the specified email.
            if (user?.Email is null || !user.EmailConfirmed)
                return Result.Success();

            var token = await userManager.GeneratePasswordResetTokenAsync(user);

            await publishEndpoint.Publish(new ForgotPasswordEvent
                {
                    Email = request.Email,
                    PasswordResetToken = token
                },
                cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("forgotPassword", async (string email, string? turnstileToken, ISender sender) =>
                {
                    var command = new Command(email, turnstileToken);

                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .WithTags(nameof(Profile))
                .RequireRateLimiting(Consts.AntiEmailAbuse);
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
        }
    }
}