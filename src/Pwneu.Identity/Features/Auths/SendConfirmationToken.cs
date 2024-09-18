using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Identity.Shared.Options;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Identity.Features.Auths;

public static class SendConfirmationToken
{
    public record Command(string Email) : IRequest<Result>;

    private static readonly Error NotRequired =
        new("SendConfirmationToken.NotRequired", "Email verification is not required");

    internal sealed class Handler(
        UserManager<User> userManager,
        IPublishEndpoint publishEndpoint,
        IOptions<AppOptions> appOptions)
        : IRequestHandler<Command, Result>
    {
        private readonly AppOptions _appOptions = appOptions.Value;

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (!_appOptions.RequireEmailVerification)
                return Result.Failure(NotRequired);

            var user = await userManager.FindByEmailAsync(request.Email);

            // Don't give the requester a clue if the user exists with the specified email.
            if (user?.Email is null || user.EmailConfirmed)
                return Result.Success();

            var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);

            await publishEndpoint.Publish(new RegisteredEvent
            {
                Email = user.Email,
                ConfirmationToken = confirmationToken
            }, cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("resend", async (string email, ISender sender) =>
                {
                    var command = new Command(email);
                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .WithTags(nameof(Auths))
                .RequireRateLimiting(Consts.AntiEmailAbuse);
        }
    }
}