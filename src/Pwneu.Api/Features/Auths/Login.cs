using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.Features.Auths;

public static class Login
{
    public record Command(string UserName, string Password) : IRequest<Result<string>>;

    private static readonly Error Invalid = new("Login.Invalid", "Incorrect username or password");

    internal sealed class Handler(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IValidator<Command> validator)
        : IRequestHandler<Command, Result<string>>
    {
        public async Task<Result<string>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<string>(new Error("Login.Validation", validationResult.ToString()));

            var user = await userManager
                .Users
                .FirstOrDefaultAsync(u => u.UserName == request.UserName, cancellationToken: cancellationToken);

            if (user is null) return Result.Failure<string>(Invalid);

            var signIn = await signInManager.CheckPasswordSignInAsync(user, request.Password, false);

            if (!signIn.Succeeded) return Result.Failure<string>(Invalid);

            var roles = await userManager.GetRolesAsync(user);
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new(JwtRegisteredClaimNames.Name, user.UserName ?? string.Empty),
                new(JwtRegisteredClaimNames.Sub, user.Id),
            };

            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Envs.JwtSigningKey()));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = credentials,
                Issuer = Envs.JwtIssuer(),
                Audience = Envs.JwtAudience()
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("login", async (LoginRequest request, ISender sender) =>
                {
                    var command = new Command(request.UserName, request.Password);
                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
                })
                .WithTags(nameof(Auths));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.UserName)
                .NotEmpty()
                .WithMessage("Username is required.");

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
        }
    }
}