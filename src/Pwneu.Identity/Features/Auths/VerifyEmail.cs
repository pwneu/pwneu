using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Identity.Features.Auths;

public class VerifyEmail
{
    public record Command(string Email, string ConfirmationToken) : IRequest<Result>;

    private static readonly Error Failed = new("VerifyEmail.Failed", "Unable to verify email");

    internal sealed class Handler(UserManager<User> userManager)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByEmailAsync(request.Email);

            if (user is null)
                return Result.Failure(Failed);

            var isVerified = await userManager.ConfirmEmailAsync(user, request.ConfirmationToken);

            return isVerified.Succeeded
                ? Result.Success()
                : Result.Failure(Failed);
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
                .WithTags(nameof(Auths));
        }
    }
}