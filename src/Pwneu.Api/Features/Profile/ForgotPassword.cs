using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Entities;
using Pwneu.Api.Services;

namespace Pwneu.Api.Features.Profile;

public static class ForgotPassword
{
    public record Command(string Email, string? TurnstileToken = null) : IRequest<Result>;

    private static readonly Error InvalidAntiSpamToken = new(
        "ForgotPassword.InvalidAntiSpamToken",
        "Invalid turnstile token. Rejecting request"
    );

    internal sealed class Handler(
        UserManager<User> userManager,
        ITurnstileValidator turnstileValidator,
        IPublishEndpoint publishEndpoint,
        IValidator<Command> validator
    ) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure(
                    new Error("ForgotPassword.Validation", validationResult.ToString())
                );

            var isValidTurnstileToken = await turnstileValidator.IsValidTurnstileTokenAsync(
                request.TurnstileToken,
                cancellationToken
            );

            if (!isValidTurnstileToken)
                return Result.Failure(InvalidAntiSpamToken);

            var user = await userManager.FindByEmailAsync(request.Email);

            // Don't give the requester a clue if the user exists with the specified email.
            if (user?.Email is null || !user.EmailConfirmed)
                return Result.Success();

            // Admin can't use this feature.
            var userIsAdmin = await userManager.IsInRoleAsync(user, Roles.Admin);
            if (userIsAdmin)
                return Result.Success();

            var token = await userManager.GeneratePasswordResetTokenAsync(user);

            await publishEndpoint.Publish(
                new ForgotPasswordEvent { Email = request.Email, PasswordResetToken = token },
                cancellationToken
            );

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "identity/forgotPassword",
                    async (string email, string? turnstileToken, ISender sender) =>
                    {
                        var command = new Command(email, turnstileToken);

                        var result = await sender.Send(command);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.NoContent();
                    }
                )
                .WithTags(nameof(Profile))
                .RequireRateLimiting(RateLimitingPolicies.AntiEmailAbuse);
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
