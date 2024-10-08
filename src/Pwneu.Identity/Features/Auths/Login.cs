using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Identity.Shared.Extensions;
using Pwneu.Identity.Shared.Options;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.Auths;

public static class Login
{
    public record Command(
        string UserName,
        string Password,
        string? TurnstileToken = null,
        string? IpAddress = null,
        string? UserAgent = null,
        string? Referer = null)
        : IRequest<Result<LoginResponse>>;

    private static readonly Error Invalid = new("Login.Invalid", "Incorrect username or password");

    private static readonly Error EmailNotConfirmed = new("Login.EmailNotConfirmed",
        "Email is not confirmed");

    private static readonly Error IpLocked = new("Login.IpLocked",
        "Ip address was locked. Please wait for a few minutes");

    private static readonly Error InvalidAntiSpamToken = new("Login.InvalidAntiSpamToken",
        "Invalid turnstileToken token. Denying login");

    internal sealed class Handler(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ApplicationDbContext context,
        IOptions<JwtOptions> jwtOptions,
        IOptions<AppOptions> appOptions,
        IPublishEndpoint publishEndpoint,
        IFusionCache cache,
        HttpClient httpClient,
        IValidator<Command> validator)
        : IRequestHandler<Command, Result<LoginResponse>>
    {
        private readonly JwtOptions _jwtOptions = jwtOptions.Value;
        private readonly AppOptions _appOptions = appOptions.Value;

        public async Task<Result<LoginResponse>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<LoginResponse>(new Error("Login.Validation", validationResult.ToString()));

            var isTurnstileEnabled = await cache.GetOrSetAsync(Keys.IsTurnstileEnabled(), async _ =>
                    await context.GetIdentityConfigurationValueAsync<bool>(
                        Consts.IsTurnstileEnabled,
                        cancellationToken),
                token: cancellationToken);

            // Validate Turnstile from Cloudflare.
            if (isTurnstileEnabled)
            {
                var validateTurnstileToken = await httpClient.PostAsync(
                    Consts.TurnstileChallengeUrl,
                    new StringContent(JsonSerializer.Serialize(new
                        {
                            secret = _appOptions.TurnstileSecretKey,
                            response = request.TurnstileToken
                        }),
                        Encoding.UTF8,
                        "application/json"
                    ), cancellationToken);

                if (!validateTurnstileToken.IsSuccessStatusCode)
                    return Result.Failure<LoginResponse>(InvalidAntiSpamToken);

                var validationResponseBody = await validateTurnstileToken.Content.ReadAsStringAsync(cancellationToken);
                var validationResponse = JsonSerializer.Deserialize<TurnstileResponse>(validationResponseBody);

                if (validationResponse is null)
                    return Result.Failure<LoginResponse>(InvalidAntiSpamToken);

                if (!validationResponse.Success)
                    return Result.Failure<LoginResponse>(InvalidAntiSpamToken);
            }

            // Validate IP Address.
            if (request.IpAddress is not null)
            {
                var ipFailedLoginCount = await cache.GetOrDefaultAsync<int>(
                    Keys.FailedLoginCount(request.IpAddress),
                    token: cancellationToken);

                if (ipFailedLoginCount > 5)
                    return Result.Failure<LoginResponse>(IpLocked);

                await cache.SetAsync(
                    Keys.FailedLoginCount(request.IpAddress),
                    ipFailedLoginCount + 1,
                    new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(5) },
                    cancellationToken);
            }

            var user = await userManager
                .Users
                .FirstOrDefaultAsync(u => u.UserName == request.UserName, cancellationToken);

            if (user is null)
                return Result.Failure<LoginResponse>(Invalid);

            var signIn = await signInManager.CheckPasswordSignInAsync(user, request.Password, false);

            if (!signIn.Succeeded)
            {
                if (!await userManager.IsEmailConfirmedAsync(user))
                    return Result.Failure<LoginResponse>(EmailNotConfirmed);

                return Result.Failure<LoginResponse>(Invalid);
            }

            var userRoles = await userManager.GetRolesAsync(user);
            var refreshTokenClaims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Name, user.UserName ?? string.Empty),
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            refreshTokenClaims.AddRange(userRoles.Select(role => new Claim(ClaimTypes.Role, role)));

            var refreshTokenKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
            var refreshTokenCredentials = new SigningCredentials(
                refreshTokenKey,
                SecurityAlgorithms.HmacSha256Signature);

            var refreshTokenJwt = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: refreshTokenClaims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: refreshTokenCredentials);

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

            var accessTokenKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
            var accessTokenCredentials = new SigningCredentials(accessTokenKey, SecurityAlgorithms.HmacSha256Signature);

            var accessToken = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: accessTokenClaims,
                notBefore: null,
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: accessTokenCredentials);

            await publishEndpoint.Publish(new LoggedInEvent
            {
                FullName = user.FullName,
                Email = user.Email,
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                Referer = request.Referer
            }, cancellationToken);

            await cache.RemoveAsync(Keys.UserToken(user.Id), token: cancellationToken);

            return new LoginResponse
            {
                Id = user.Id,
                UserName = user.UserName,
                Roles = userRoles.ToList(),
                AccessToken = new JwtSecurityTokenHandler().WriteToken(accessToken),
                RefreshToken = refreshToken
            };
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("login", async (LoginRequest request, HttpContext httpContext, ISender sender) =>
                {
                    var ipAddress = httpContext.Request.Headers["X-Forwarded-For"].ToString();
                    // var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                    var userAgent = httpContext.Request.Headers.UserAgent.ToString();
                    var referer = httpContext.Request.Headers.Referer.ToString();

                    var command = new Command(request.UserName, request.Password, request.TurnstileToken, ipAddress,
                        userAgent, referer);
                    var result = await sender.Send(command);

                    if (result.IsFailure)
                        return Results.BadRequest(result.Error);

                    var response = result.Value;

                    var cookieOptions = new CookieOptions
                    {
                        Expires = DateTime.Now.AddDays(7),
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict
                    };

                    httpContext.Response.Cookies.Append(Consts.RefreshToken, response.RefreshToken, cookieOptions);

                    return Results.Ok(new TokenResponse
                    {
                        Id = response.Id,
                        UserName = response.UserName,
                        Roles = response.Roles,
                        AccessToken = response.AccessToken
                    });
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