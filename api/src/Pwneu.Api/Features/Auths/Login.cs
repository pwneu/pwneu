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

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.UserName).NotEmpty();
            RuleFor(c => c.Password).NotEmpty();
        }
    }

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
                return Result.Failure<string>(new Error("CreateUser.Validation", validationResult.ToString()));

            var user = await userManager
                .Users
                .FirstOrDefaultAsync(u => u.UserName == request.UserName, cancellationToken: cancellationToken);

            if (user is null)
                return Result.Failure<string>(new Error("Login.InvalidCredentials",
                    "Incorrect username or password"));

            var signIn = await signInManager.CheckPasswordSignInAsync(user, request.Password, false);

            if (!signIn.Succeeded)
                return Result.Failure<string>(new Error("Login.InvalidCredentials",
                    "Incorrect username or password"));

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new(JwtRegisteredClaimNames.Name, user.UserName ?? string.Empty),
                new(JwtRegisteredClaimNames.Sub, user.Id),
            };

            var issuer = Environment.GetEnvironmentVariable(Env.JwtIssuer);
            var audience = Environment.GetEnvironmentVariable(Env.JwtAudience);
            var signingKey = Environment.GetEnvironmentVariable(Env.JwtSigningKey);

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = credentials,
                Issuer = issuer,
                Audience = audience
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

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .WithTags("Auth");
        }
    }
}