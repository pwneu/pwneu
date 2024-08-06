using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using Pwneu.Api.Shared.Options;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Auths;

public static class Register
{
    private static readonly Error Failed = new("Register.Failed", "Unable to create user");
    private static readonly Error AddRoleFailed = new("Register.AddRoleFailed", "Unable to add role to user");

    public record Command(string UserName, string Email, string Password, string FullName, string? AccessKey = null)
        : IRequest<Result>;

    internal sealed class Handler(
        ApplicationDbContext context,
        UserManager<User> userManager,
        IFusionCache cache,
        IValidator<Command> validator,
        IOptions<AppOptions> appOptions) : IRequestHandler<Command, Result>
    {
        private readonly AppOptions _appOptions = appOptions.Value;

        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure(new Error("Register.Validation", validationResult.ToString()));

            List<AccessKey>? accessKeys = default;
            AccessKey? accessKey = default;

            // Check if a registration key is required.
            if (_appOptions.RequireRegistrationKey)
            {
                // Set values of the variables above.
                accessKeys = await cache.GetOrSetAsync(Keys.AccessKeys(), async _ =>
                    await context
                        .AccessKeys
                        .ToListAsync(cancellationToken), token: cancellationToken);

                accessKey = accessKeys.FirstOrDefault(a =>
                    a.Id.ToString() == request.AccessKey && a.Expiration > DateTime.UtcNow);

                // If no access key matched, don't register the user.
                if (accessKey is null)
                    return Result.Failure(Failed);
            }

            var user = new User
            {
                UserName = request.UserName,
                Email = request.Email,
                FullName = request.FullName,
                CreatedAt = DateTime.UtcNow
            };

            var createUser = await userManager.CreateAsync(user, request.Password);
            if (!createUser.Succeeded)
                return Result.Failure(Failed);

            var addRole = await userManager.AddToRoleAsync(user, Consts.Member);
            if (!addRole.Succeeded)
                return Result.Failure(AddRoleFailed);

            // If a registration key is not required, accessKey and accessKeys should be null at this point.
            // But still check if the values are null just in case.
            if (!_appOptions.RequireRegistrationKey || accessKey is null || accessKeys is null || accessKey.CanBeReused)
                return Result.Success();

            // Remove key since it cannot be reused.
            context.AccessKeys.Remove(accessKey);
            await context.SaveChangesAsync(cancellationToken);

            // Update cache by removing the used key in the key list.
            await cache.SetAsync(Keys.AccessKeys(), accessKeys.Where(a => a.Id != accessKey.Id).ToList(),
                token: cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("register", async (RegisterRequest request, ISender sender) =>
                {
                    var command = new Command(request.UserName, request.Email, request.Password, request.FullName,
                        request.AccessKey);
                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .WithTags(nameof(Auths));
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

            RuleFor(c => c.Password)
                .NotEmpty()
                .WithMessage("Password is required.")
                .MinimumLength(12)
                .WithMessage("Password must be at least 12 characters long.")
                .Matches(@"[A-Z]")
                .WithMessage("Password must contain at least one uppercase letter.")
                .Matches(@"[a-z]")
                .WithMessage("Password must contain at least one lowercase letter.")
                .Matches(@"[0-9]")
                .WithMessage("Password must contain at least one digit.")
                .Matches(@"[\W_]")
                .WithMessage("Password must contain at least one non-alphanumeric character.");

            RuleFor(c => c.FullName)
                .NotEmpty()
                .WithMessage("Full Name is required.")
                .MaximumLength(100)
                .WithMessage("Full Name must be 100 characters or less.");
        }
    }
}