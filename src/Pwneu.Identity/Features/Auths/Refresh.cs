using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Identity.Shared.Options;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;

namespace Pwneu.Identity.Features.Auths;

public static class Refresh
{
    public record Command(string? RefreshToken) : IRequest<Result<TokenResponse>>;

    private static readonly Error Invalid = new("Refresh.Invalid", "Invalid token");

    internal sealed class Handler(
        UserManager<User> userManager,
        IOptions<JwtOptions> jwtOptions,
        IValidator<Command> validator)
        : IRequestHandler<Command, Result<TokenResponse>>
    {
        private readonly JwtOptions _jwtOptions = jwtOptions.Value;

        public async Task<Result<TokenResponse>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<TokenResponse>(new Error("Refresh.Validation", validationResult.ToString()));

            try
            {
                // Validate the refresh token
                var validationParameters = new TokenValidationParameters
                {
                    ValidIssuer = _jwtOptions.Issuer,
                    ValidAudience = _jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)),
                    ValidateLifetime = true
                };

                var principal = new JwtSecurityTokenHandler().ValidateToken(
                    request.RefreshToken,
                    validationParameters,
                    out var validatedToken);

                if (validatedToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(
                        SecurityAlgorithms.HmacSha256Signature,
                        StringComparison.InvariantCultureIgnoreCase))
                    return Result.Failure<TokenResponse>(Invalid);

                // Extract user information from the claims
                var userId = principal.GetLoggedInUserId<string>();
                if (userId is null)
                    return Result.Failure<TokenResponse>(Invalid);

                var user = await userManager.FindByIdAsync(userId);

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

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
                var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

                var accessToken = new JwtSecurityToken(
                    issuer: _jwtOptions.Issuer,
                    audience: _jwtOptions.Audience,
                    claims: claims,
                    notBefore: null,
                    expires: DateTime.UtcNow.AddHours(1),
                    signingCredentials: credentials);

                return new TokenResponse
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Roles = roles.ToList(),
                    AccessToken = new JwtSecurityTokenHandler().WriteToken(accessToken)
                };
            }
            catch (SecurityTokenException)
            {
                return Result.Failure<TokenResponse>(Invalid);
            }
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("refresh", async (HttpContext httpContext, ISender sender) =>
                {
                    var refreshToken = httpContext.Request.Cookies[Consts.RefreshToken];

                    var command = new Command(refreshToken);
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
            RuleFor(c => c.RefreshToken)
                .NotEmpty()
                .WithMessage("Refresh Token is required.");
        }
    }
}