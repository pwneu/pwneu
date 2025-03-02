using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Entities;
using Pwneu.Api.Services;

namespace Pwneu.Api.Features.Auths;

public static class ResendConfirmationToken
{
    public record Command(string Email, string? TurnstileToken = null) : IRequest<Result>;

    private static readonly Error InvalidAntiSpamToken = new(
        "SendConfirmationToken.InvalidAntiSpamToken",
        "Invalid turnstile token. Rejecting request"
    );

    internal sealed class Handler(
        UserManager<User> userManager,
        ITurnstileValidator turnstileValidator,
        IMediator mediator
    ) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var isValidTurnstileToken = await turnstileValidator.IsValidTurnstileTokenAsync(
                request.TurnstileToken,
                cancellationToken
            );

            if (!isValidTurnstileToken)
                return Result.Failure(InvalidAntiSpamToken);

            var user = await userManager.FindByEmailAsync(request.Email);

            // Don't give the requester a clue if the user exists with the specified email.
            if (user?.Email is null || user.EmailConfirmed)
                return Result.Success();

            var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);

            await mediator.Publish(
                new RegisteredEvent
                {
                    UserName = user.UserName ?? "User",
                    Email = user.Email,
                    FullName = user.FullName,
                    ConfirmationToken = confirmationToken,
                    IpAddress = user.RegistrationIpAddress,
                },
                cancellationToken
            );

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "identity/resend",
                    async (string email, string? turnstileToken, ISender sender) =>
                    {
                        var command = new Command(email, turnstileToken);
                        var result = await sender.Send(command);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.NoContent();
                    }
                )
                .RequireRateLimiting(RateLimitingPolicies.AntiEmailAbuse)
                .WithTags(nameof(Auths));
        }
    }
}
