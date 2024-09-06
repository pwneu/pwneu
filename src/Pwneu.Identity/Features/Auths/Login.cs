using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Identity.Shared.Options;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Identity.Features.Auths;

// TODO -- Automatically send confirmation token on logged in on unverified email
// TODO -- Fix login trace

public static class Login
{
    public record Command(
        string UserName,
        string Password,
        string? IpAddress = null,
        string? UserAgent = null,
        string? Referer = null)
        : IRequest<Result<TokenResponse>>;

    private static readonly Error Invalid = new("Login.Invalid", "Incorrect username or password");

    private static readonly Error EmailNotConfirmed = new("Login.EmailNotConfirmed",
        "Email is not confirmed");

    internal sealed class Handler(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IOptions<JwtOptions> jwtOptions,
        IPublishEndpoint publishEndpoint,
        IValidator<Command> validator)
        : IRequestHandler<Command, Result<TokenResponse>>
    {
        private readonly JwtOptions _jwtOptions = jwtOptions.Value;

        public async Task<Result<TokenResponse>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<TokenResponse>(new Error("Login.Validation", validationResult.ToString()));

            var user = await userManager
                .Users
                .FirstOrDefaultAsync(u => u.UserName == request.UserName, cancellationToken: cancellationToken);

            if (user is null)
                return Result.Failure<TokenResponse>(Invalid);

            var signIn = await signInManager.CheckPasswordSignInAsync(user, request.Password, false);

            if (!signIn.Succeeded)
            {
                if (!await userManager.IsEmailConfirmedAsync(user))
                    return Result.Failure<TokenResponse>(EmailNotConfirmed);

                return Result.Failure<TokenResponse>(Invalid);
            }

            string refreshToken;
            using (var rng = RandomNumberGenerator.Create())
            {
                var randomNumber = new byte[64];
                rng.GetBytes(randomNumber);
                refreshToken = Convert.ToBase64String(randomNumber);
            }

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

            await userManager.UpdateAsync(user);

            var roles = await userManager.GetRolesAsync(user);
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Name, user.UserName ?? string.Empty),
                new(JwtRegisteredClaimNames.Sub, user.Id),
            };
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

            var accessToken = new JwtSecurityToken(_jwtOptions.Issuer, _jwtOptions.Audience, claims, null,
                DateTime.UtcNow.AddHours(1), credentials);

            await publishEndpoint.Publish(new LoggedInEvent
            {
                FullName = user.FullName,
                Email = user.Email,
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                Referer = request.Referer
            }, cancellationToken);

            return new TokenResponse
            {
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
                    // var ipAddress = httpContext.Request.Headers["X-Forwarded-For"].ToString();
                    var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                    var userAgent = httpContext.Request.Headers.UserAgent.ToString();
                    var referer = httpContext.Request.Headers.Referer.ToString();

                    var command = new Command(request.UserName, request.Password, ipAddress, userAgent, referer);
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