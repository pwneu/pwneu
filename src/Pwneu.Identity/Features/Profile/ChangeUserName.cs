using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.Profile;

public static class ChangeUserName
{
    public record Command(string UserId, string NewUserName) : IRequest<Result>;

    private static readonly Error NotFound = new("ChangeUserName.NotFound",
        "The user with the specified ID was not found");

    private static readonly Error Failed = new("ChangeUserName.Failed", "Unable to change user name.");

    internal sealed class Handler(
        UserManager<User> userManager,
        IValidator<Command> validator,
        IFusionCache cache) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(new Error("ChangeUserName.Validation", validationResult.ToString()));

            var user = await userManager.FindByIdAsync(request.UserId);

            if (user is null)
                return Result.Failure(NotFound);

            var updateUserName = await userManager.SetUserNameAsync(user, request.NewUserName);

            if (!updateUserName.Succeeded)
                return Result.Failure(Failed);

            var invalidationTasks = new List<Task>
            {
                cache.RemoveAsync(Keys.User(request.UserId), token: cancellationToken).AsTask(),
                cache.RemoveAsync(Keys.UserDetails(request.UserId), token: cancellationToken).AsTask()
            };

            await Task.WhenAll(invalidationTasks);

            return Result.Success();
        }
    }

    // TODO -- Disable this
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("me/userName", async (string newUserName, ClaimsPrincipal claims, ISender sender) =>
                {
                    var userId = claims.GetLoggedInUserId<string>();
                    if (userId is null) return Results.BadRequest();

                    var command = new Command(userId, newUserName);

                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization()
                .WithTags(nameof(Profile));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.UserId)
                .NotEmpty()
                .WithMessage("User ID is required.");

            RuleFor(c => c.NewUserName)
                .NotEmpty()
                .WithMessage("New User Name is required.")
                .MaximumLength(100)
                .WithMessage("New User Name must be 100 characters or less.");
        }
    }
}