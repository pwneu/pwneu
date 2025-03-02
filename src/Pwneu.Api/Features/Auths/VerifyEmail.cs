using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Auths;

public static class VerifyEmail
{
    public record Command(string Email, string ConfirmationToken) : IRequest<Result>;

    private static readonly Error Failed = new("VerifyEmail.Failed", "Unable to verify email");

    private static readonly Error EmailAlreadyConfirmed = new(
        "VerifyEmail.EmailAlreadyConfirmed",
        "Email is already confirmed."
    );

    internal sealed class Handler(
        UserManager<User> userManager,
        IFusionCache cache,
        ILogger<Handler> logger
    ) : IRequestHandler<Command, Result>
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
                    .RemoveAsync(CacheKeys.UserDetailsNoEmail(user.Id), token: cancellationToken)
                    .AsTask(),
            };

            await Task.WhenAll(invalidationTasks);

            logger.LogInformation("User verified: {Email}", request.Email);

            return Result.Failure(Failed);
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "identity/verify",
                    async (VerifyEmailRequest request, ISender sender) =>
                    {
                        var command = new Command(request.Email, request.ConfirmationToken);
                        var result = await sender.Send(command);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.NoContent();
                    }
                )
                .RequireRateLimiting(RateLimitingPolicies.VerifyEmail)
                .WithTags(nameof(Auths));
        }
    }
}
