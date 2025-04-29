﻿using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions;
using Pwneu.Api.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Auths;

public static class Refresh
{
    public record Command(string? RefreshToken) : IRequest<Result<TokenResponse>>;

    private static readonly Error Invalid = new("Refresh.Invalid", "Invalid token");

    private static readonly Error InArchiveMode = new(
        "Refresh.InArchiveMode",
        "The platform is currently in archive mode. Authentication is disabled."
    );

    internal sealed class Handler(
        AppDbContext context,
        IOptions<JwtOptions> jwtOptions,
        IOptions<AppOptions> appOptions,
        IFusionCache cache,
        IValidator<Command> validator
    ) : IRequestHandler<Command, Result<TokenResponse>>
    {
        private readonly JwtOptions _jwtOptions = jwtOptions.Value;
        private readonly AppOptions _appOptions = appOptions.Value;

        public async Task<Result<TokenResponse>> Handle(
            Command request,
            CancellationToken cancellationToken
        )
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<TokenResponse>(
                    new Error("Refresh.Validation", validationResult.ToString())
                );

            if (_appOptions.IsArchiveMode)
                return Result.Failure<TokenResponse>(InArchiveMode);

            try
            {
                // Validate the refresh token
                var validationParameters = new TokenValidationParameters
                {
                    ValidIssuer = _jwtOptions.Issuer,
                    ValidAudience = _jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(_jwtOptions.RefreshTokenSigningKey)
                    ),
                    ValidateLifetime = true,
                };

                var principal = new JwtSecurityTokenHandler().ValidateToken(
                    request.RefreshToken,
                    validationParameters,
                    out var validatedToken
                );

                if (
                    validatedToken is not JwtSecurityToken jwtSecurityToken
                    || !jwtSecurityToken.Header.Alg.Equals(
                        SecurityAlgorithms.HmacSha256Signature,
                        StringComparison.InvariantCultureIgnoreCase
                    )
                )
                    return Result.Failure<TokenResponse>(Invalid);

                // Extract user information from the claims
                var userId = principal.GetLoggedInUserId<string>();
                var userName = principal.GetLoggedInUserName();
                var roles = principal.GetRoles().ToList();

                if (userId is null || userName is null || roles.Count == 0)
                    return Result.Failure<TokenResponse>(Invalid);

                var userToken = await cache.GetOrSetAsync(
                    CacheKeys.UserToken(userId),
                    async _ =>
                    {
                        return await context
                            .Users.Where(u => u.Id == userId)
                            .Select(u => new UserTokenResponse
                            {
                                RefreshToken = u.RefreshToken,
                                RefreshTokenExpiry = u.RefreshTokenExpiry,
                            })
                            .FirstOrDefaultAsync(cancellationToken);
                    },
                    new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(15) },
                    cancellationToken
                );

                if (
                    userToken is null
                    || userToken.RefreshToken != request.RefreshToken
                    || userToken.RefreshTokenExpiry < DateTime.UtcNow
                )
                    return Result.Failure<TokenResponse>(Invalid);

                var claims = new List<Claim>
                {
                    new(JwtRegisteredClaimNames.Name, userName),
                    new(JwtRegisteredClaimNames.Sub, userId),
                };
                claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
                var credentials = new SigningCredentials(
                    key,
                    SecurityAlgorithms.HmacSha256Signature
                );

                var accessToken = new JwtSecurityToken(
                    issuer: _jwtOptions.Issuer,
                    audience: _jwtOptions.Audience,
                    claims: claims,
                    notBefore: null,
                    expires: DateTime.UtcNow.AddMinutes(15),
                    signingCredentials: credentials
                );

                return new TokenResponse
                {
                    Id = userId,
                    UserName = userName,
                    Roles = roles,
                    AccessToken = new JwtSecurityTokenHandler().WriteToken(accessToken),
                };
            }
            catch (SecurityTokenException)
            {
                return Result.Failure<TokenResponse>(Invalid);
            }
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "identity/refresh",
                    async (HttpContext httpContext, ISender sender) =>
                    {
                        var refreshToken = httpContext.Request.Cookies[
                            CommonConstants.RefreshToken
                        ];

                        var command = new Command(refreshToken);
                        var result = await sender.Send(command);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.Ok(result.Value);
                    }
                )
                .WithTags(nameof(Auths));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.RefreshToken).NotEmpty().WithMessage("Refresh Token is required.");
        }
    }
}
