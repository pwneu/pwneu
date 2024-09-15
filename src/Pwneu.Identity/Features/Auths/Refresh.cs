using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Options;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.Auths;

public static class Refresh
{
    public record Command(string? RefreshToken) : IRequest<Result<TokenResponse>>;

    private static readonly Error Invalid = new("Refresh.Invalid", "Invalid token");

    internal sealed class Handler(
        ApplicationDbContext context,
        IOptions<JwtOptions> jwtOptions,
        IFusionCache cache,
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
                var userName = principal.GetLoggedInUserName();
                var roles = principal.GetRoles().ToList();

                if (userId is null || userName is null || roles.Count == 0)
                    return Result.Failure<TokenResponse>(Invalid);

                var userToken = await cache.GetOrSetAsync(Keys.UserToken(userId), async _ =>
                    {
                        return await context
                            .Users
                            .Where(u => u.Id == userId)
                            .Select(u => new UserTokenResponse
                            {
                                RefreshToken = u.RefreshToken,
                                RefreshTokenExpiry = u.RefreshTokenExpiry
                            })
                            .FirstOrDefaultAsync(cancellationToken);
                    },
                    new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(15) },
                    cancellationToken);

                if (userToken is null ||
                    userToken.RefreshToken != request.RefreshToken ||
                    userToken.RefreshTokenExpiry < DateTime.UtcNow)
                    return Result.Failure<TokenResponse>(Invalid);

                var claims = new List<Claim>
                {
                    new(JwtRegisteredClaimNames.Name, userName),
                    new(JwtRegisteredClaimNames.Sub, userId),
                };

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
                var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

                var accessToken = new JwtSecurityToken(
                    issuer: _jwtOptions.Issuer,
                    audience: _jwtOptions.Audience,
                    claims: claims,
                    notBefore: null,
                    expires: DateTime.UtcNow.AddMinutes(15),
                    signingCredentials: credentials);

                return new TokenResponse
                {
                    Id = userId,
                    UserName = userName,
                    Roles = roles,
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
            app.MapGet("refresh", async (HttpContext httpContext, ISender sender) =>
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