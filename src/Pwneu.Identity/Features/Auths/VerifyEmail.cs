using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.Auths;

public static class VerifyEmail
{
    public record Command(string Email, string ConfirmationToken) : IRequest<Result>;

    private static readonly Error Failed = new("VerifyEmail.Failed", "Unable to verify email");

    private static readonly Error EmailAlreadyConfirmed = new("VerifyEmail.EmailAlreadyConfirmed",
        "Email is already confirmed.");

    internal sealed class Handler(
        UserManager<User> userManager,
        ILogger<Handler> logger,
        IFusionCache cache)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByEmailAsync(request.Email);

            if (user is null)
                return Result.Failure(Failed);

            if (user.EmailConfirmed)
                return Result.Failure(EmailAlreadyConfirmed);

            var verifyEmail = await userManager.ConfirmEmailAsync(user, request.ConfirmationToken);

            if (verifyEmail.Succeeded)
                return Result.Success();

            var invalidationTasks = new List<Task>
            {
                cache
                    .RemoveAsync(Keys.UserDetails(user.Id), token: cancellationToken)
                    .AsTask(),
            };

            await Task.WhenAll(invalidationTasks);

            logger.LogInformation("User verified: {Email}", request.Email);

            return Result.Failure(Failed);
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("verify", async (ConfirmEmailRequest request, ISender sender) =>
                {
                    var command = new Command(request.Email, request.ConfirmationToken);
                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireRateLimiting(Consts.VerifyEmail)
                .WithTags(nameof(Auths));
        }
    }
}