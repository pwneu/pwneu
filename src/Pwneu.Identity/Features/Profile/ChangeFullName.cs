using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.Profile;

public static class ChangeFullName
{
    public record Command(string UserId, string NewFullName) : IRequest<Result>;

    private static readonly Error NotFound = new("ChangeFullName.NotFound",
        "The user with the specified ID was not found");

    private static readonly Error Failed = new("ChangeFullName.Failed", "Unable to change full name.");

    internal sealed class Handler(
        UserManager<User> userManager,
        IValidator<Command> validator,
        IFusionCache cache) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(new Error("ChangeFullName.Validation", validationResult.ToString()));

            var user = await userManager.FindByIdAsync(request.UserId);

            if (user is null)
                return Result.Failure(NotFound);

            user.FullName = request.NewFullName;

            var updateFullName = await userManager.UpdateAsync(user);

            if (!updateFullName.Succeeded)
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

    public class Endpoint // : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("me/fullName", async (string newFullName, ClaimsPrincipal claims, ISender sender) =>
                {
                    var userId = claims.GetLoggedInUserId<string>();
                    if (userId is null) return Results.BadRequest();

                    var command = new Command(userId, newFullName);

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

            RuleFor(c => c.NewFullName)
                .NotEmpty()
                .WithMessage("New Full Name is required.")
                .MaximumLength(100)
                .WithMessage("New Full Name must be 100 characters or less.");
        }
    }
}