using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.Features.Auths;

public static class Register
{
    public record Command(string UserName, string Email, string Password, string FullName) : IRequest<Result>;

    private static readonly Error Failed = new("Register.Failed", "Unable to create user");
    private static readonly Error AddRoleFailed = new("Register.AddRoleFailed", "Unable to add role to user");

    // TODO: Create a register request using register key
    internal sealed class Handler(UserManager<User> userManager, IValidator<Command> validator)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure(new Error("Register.Validation", validationResult.ToString()));

            var user = new User
            {
                UserName = request.UserName,
                Email = request.Email,
                FullName = request.FullName,
                CreatedAt = DateTime.UtcNow,
            };

            var createUser = await userManager.CreateAsync(user, request.Password);
            if (!createUser.Succeeded)
                return Result.Failure(Failed);

            var addRole = await userManager.AddToRoleAsync(user, Consts.Member);
            return !addRole.Succeeded
                ? Result.Failure(AddRoleFailed)
                : Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("register", async (RegisterRequest request, ISender sender) =>
                {
                    var command = new Command(request.UserName, request.Email, request.Password, request.FullName);
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