using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Entities;
using Pwneu.Api.Shared.Extensions;

namespace Pwneu.Api.Features.Auths;

public static class Refresh
{
    public record Command(string AccessToken, string RefreshToken) : IRequest<Result<TokenResponse>>;

    private static readonly Error Invalid = new("Refresh.Invalid", "Invalid token");

    internal sealed class Handler(
        UserManager<User> userManager,
        IValidator<Command> validator)
        : IRequestHandler<Command, Result<TokenResponse>>
    {
        public async Task<Result<TokenResponse>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<TokenResponse>(new Error("Refresh.Validation", validationResult.ToString()));

            var validation = new TokenValidationParameters
            {
                ValidIssuer = Envs.JwtIssuer(),
                ValidAudience = Envs.JwtAudience(),
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Envs.JwtSigningKey())),
                ValidateLifetime = false
            };

            var principal = new JwtSecurityTokenHandler().ValidateToken(request.AccessToken, validation, out _);

            var userName = principal.GetLoggedInUserName();
            if (userName is null)
                return Result.Failure<TokenResponse>(Invalid);

            var user = await userManager.FindByNameAsync(userName);

            if (user is null ||
                user.RefreshToken != request.RefreshToken ||
                user.RefreshTokenExpiry < DateTime.UtcNow)
                return Result.Failure<TokenResponse>(Invalid);

            var roles = await userManager.GetRolesAsync(user);
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Name, user.UserName ?? string.Empty),
                new(JwtRegisteredClaimNames.Sub, user.Id),
            };
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Envs.JwtSigningKey()));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var accessToken = new JwtSecurityToken(Envs.JwtIssuer(), Envs.JwtAudience(), claims, null,
                DateTime.UtcNow.AddHours(1), credentials);

            return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(accessToken), request.RefreshToken);
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("refresh", async (RefreshRequest request, ISender sender) =>
                {
                    var command = new Command(request.AccessToken, request.RefreshToken);
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
            RuleFor(c => c.AccessToken)
                .NotEmpty()
                .WithMessage("Access Token is required.");

            RuleFor(c => c.RefreshToken)
                .NotEmpty()
                .WithMessage("Refresh Token is required.");
        }
    }
}