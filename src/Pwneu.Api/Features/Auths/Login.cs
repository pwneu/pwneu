using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Entities;
using Pwneu.Api.Options;
using Pwneu.Api.Services;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Auths;

public static class Login
{
    public record Command(
        string UserName,
        string Password,
        string? TurnstileToken = null,
        string? IpAddress = null,
        string? UserAgent = null,
        string? Referer = null
    ) : IRequest<Result<LoginResponse>>;

    private static readonly Error Invalid = new("Login.Invalid", "Incorrect username or password");

    private static readonly Error EmailNotConfirmed = new(
        "Login.EmailNotConfirmed",
        "Email is not confirmed"
    );

    private static readonly Error IpLocked = new(
        "Login.IpLocked",
        "IP address was locked due to one or more failed login attempts. Please wait for a few minutes"
    );

    private static readonly Error InvalidAntiSpamToken = new(
        "Login.InvalidAntiSpamToken",
        "Verification failed. Rejecting Login"
    );

    private static readonly Error UserLocked = new(
        "Login.UserLocked",
        "User account locked due to one or more failed login attempts. Please wait a few minutes."
    );

    internal sealed class Handler(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ITurnstileValidator turnstileValidator,
        IFusionCache cache,
        IPublishEndpoint publishEndpoint,
        IOptions<JwtOptions> jwtOptions,
        IOptions<AppOptions> appOptions,
        IValidator<Command> validator,
        ILogger<Handler> logger
    ) : IRequestHandler<Command, Result<LoginResponse>>
    {
        private readonly JwtOptions _jwtOptions = jwtOptions.Value;

        public async Task<Result<LoginResponse>> Handle(
            Command request,
            CancellationToken cancellationToken
        )
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<LoginResponse>(
                    new Error("Login.Validation", validationResult.ToString())
                );

            logger.LogInformation(
                "User attempted to login: {UserName}, IP Address: {IpAddress}",
                request.UserName,
                request.IpAddress
            );

            // Validate Turnstile from Cloudflare.
            var isValidTurnstile = await turnstileValidator.IsValidTurnstileTokenAsync(
                request.TurnstileToken,
                cancellationToken
            );

            if (!isValidTurnstile)
                return Result.Failure<LoginResponse>(InvalidAntiSpamToken);

            var MaxFailedIpAddressAttemptCount = appOptions.Value.MaxFailedIpAddressAttemptCount;
            var MaxFailedUserAttemptCount = appOptions.Value.MaxFailedUserAttemptCount;

            // Check IP-based failed login attempts.
            if (request.IpAddress is not null)
            {
                var ipFailedLoginCount = await cache.GetOrDefaultAsync<int>(
                    CacheKeys.FailedLoginCountByIp(request.IpAddress),
                    token: cancellationToken
                );

                if (ipFailedLoginCount > MaxFailedIpAddressAttemptCount)
                    return Result.Failure<LoginResponse>(IpLocked);
            }

            var user = await userManager.Users.FirstOrDefaultAsync(
                u => u.UserName == request.UserName,
                cancellationToken
            );

            if (user is not null)
            {
                var userFailedLoginCount = await cache.GetOrDefaultAsync<int>(
                    CacheKeys.FailedLoginCountByUser(user.Id),
                    token: cancellationToken
                );

                if (userFailedLoginCount > MaxFailedUserAttemptCount)
                    return Result.Failure<LoginResponse>(UserLocked);
            }

            if (user is null)
            {
                await IncrementFailedLoginCountAsync();
                return Result.Failure<LoginResponse>(Invalid);
            }

            var signIn = await signInManager.CheckPasswordSignInAsync(
                user,
                request.Password,
                false
            );

            if (!signIn.Succeeded)
            {
                if (!await userManager.IsEmailConfirmedAsync(user))
                    return Result.Failure<LoginResponse>(EmailNotConfirmed);

                await IncrementFailedLoginCountAsync();
                return Result.Failure<LoginResponse>(Invalid);
            }

            var userRoles = await userManager.GetRolesAsync(user);
            var refreshTokenClaims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Name, user.UserName ?? string.Empty),
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };
            refreshTokenClaims.AddRange(userRoles.Select(role => new Claim(ClaimTypes.Role, role)));

            var refreshTokenKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwtOptions.RefreshTokenSigningKey)
            );

            var refreshTokenCredentials = new SigningCredentials(
                refreshTokenKey,
                SecurityAlgorithms.HmacSha256Signature
            );
            var refreshTokenJwt = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: refreshTokenClaims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: refreshTokenCredentials
            );

            var refreshToken = new JwtSecurityTokenHandler().WriteToken(refreshTokenJwt);

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

            await userManager.UpdateAsync(user);

            var accessTokenClaims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Name, user.UserName ?? string.Empty),
                new(JwtRegisteredClaimNames.Sub, user.Id),
            };
            accessTokenClaims.AddRange(userRoles.Select(role => new Claim(ClaimTypes.Role, role)));

            var accessTokenKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)
            );
            var accessTokenCredentials = new SigningCredentials(
                accessTokenKey,
                SecurityAlgorithms.HmacSha256Signature
            );

            var accessToken = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: accessTokenClaims,
                notBefore: null,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: accessTokenCredentials
            );

            await publishEndpoint.Publish(
                new LoggedInEvent
                {
                    FullName = user.FullName,
                    Email = user.Email,
                    IpAddress = request.IpAddress,
                    UserAgent = request.UserAgent,
                    Referer = request.Referer,
                },
                cancellationToken
            );

            if (request.IpAddress is not null)
                await cache.SetAsync(
                    CacheKeys.FailedLoginCountByIp(request.IpAddress),
                    0,
                    token: cancellationToken
                );

            await cache.SetAsync(
                CacheKeys.FailedLoginCountByUser(user.Id),
                0,
                token: cancellationToken
            );

            await cache.RemoveAsync(CacheKeys.UserToken(user.Id), token: cancellationToken);

            logger.LogInformation(
                "User logged in: {UserName}, IP Address: {IpAddress}",
                request.UserName,
                request.IpAddress
            );

            return new LoginResponse
            {
                Id = user.Id,
                UserName = user.UserName,
                Roles = [.. userRoles],
                AccessToken = new JwtSecurityTokenHandler().WriteToken(accessToken),
                RefreshToken = refreshToken,
            };

            // Race condition... But I don't care.
            async Task IncrementFailedLoginCountAsync()
            {
                if (request.IpAddress is not null)
                {
                    var ipFailedLoginCount = await cache.GetOrDefaultAsync<int>(
                        CacheKeys.FailedLoginCountByIp(request.IpAddress),
                        token: cancellationToken
                    );
                    await cache.SetAsync(
                        CacheKeys.FailedLoginCountByIp(request.IpAddress),
                        ipFailedLoginCount + 1,
                        new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(5) },
                        cancellationToken
                    );
                }

                if (user is not null)
                {
                    var userFailedLoginCount = await cache.GetOrDefaultAsync<int>(
                        CacheKeys.FailedLoginCountByUser(user.Id),
                        token: cancellationToken
                    );
                    await cache.SetAsync(
                        CacheKeys.FailedLoginCountByUser(user.Id),
                        userFailedLoginCount + 1,
                        new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(5) },
                        cancellationToken
                    );
                }
            }
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "identity/login",
                    async (LoginRequest request, HttpContext httpContext, ISender sender) =>
                    {
                        var ipAddress = httpContext
                            .Request.Headers[CommonConstants.CfConnectingIp]
                            .ToString();
                        // var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
                        var referer = httpContext.Request.Headers.Referer.ToString();

                        var command = new Command(
                            request.UserName,
                            request.Password,
                            request.TurnstileToken,
                            ipAddress,
                            userAgent,
                            referer
                        );
                        var result = await sender.Send(command);

                        if (result.IsFailure)
                            return Results.BadRequest(result.Error);

                        var response = result.Value;

                        var cookieOptions = new CookieOptions
                        {
                            Expires = DateTime.Now.AddDays(7),
                            HttpOnly = true,
                            Secure = true,
                            SameSite = SameSiteMode.Strict,
                        };

                        httpContext.Response.Cookies.Append(
                            CommonConstants.RefreshToken,
                            response.RefreshToken,
                            cookieOptions
                        );

                        return Results.Ok(
                            new TokenResponse
                            {
                                Id = response.Id,
                                UserName = response.UserName,
                                Roles = response.Roles,
                                AccessToken = response.AccessToken,
                            }
                        );
                    }
                )
                .WithTags(nameof(Auths));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.UserName)
                .NotEmpty()
                .WithMessage("Username is required.")
                .MinimumLength(5)
                .WithMessage("Username must be at least 5 characters long.")
                .MaximumLength(40)
                .WithMessage("Username must be 40 characters or less.")
                .Matches(@"^[a-zA-Z0-9]+$")
                .WithMessage("Username can only contain letters and numbers.");

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
