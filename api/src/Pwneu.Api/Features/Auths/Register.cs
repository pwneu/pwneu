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

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Email).NotEmpty();
            RuleFor(c => c.Password).NotEmpty();
            RuleFor(c => c.FullName).NotEmpty();
        }
    }

    // TODO: Create a register request using register key
    internal sealed class Handler(UserManager<User> userManager, IValidator<Command> validator)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure(new Error("CreateUser.Validation", validationResult.ToString()));

            var user = new User
            {
                UserName = request.UserName,
                Email = request.Email,
                FullName = request.FullName,
                CreatedAt = DateTime.UtcNow,
            };

            var createUser = await userManager.CreateAsync(user, request.Password);
            if (!createUser.Succeeded)
                return Result.Failure(new Error("CreateUser.Failed", "Unable to create user"));

            var addRole = await userManager.AddToRoleAsync(user, Constants.Roles.User);
            return !addRole.Succeeded
                ? Result.Failure(new Error("CreateUser.AddRoleFailed", "Unable to add role to user"))
                : Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("register", async (CreateUserRequest request, ISender sender) =>
                {
                    var command = new Command(request.UserName, request.Email, request.Password, request.FullName);
                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .WithTags("Auth");
        }
    }
}