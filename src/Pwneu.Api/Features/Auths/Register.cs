using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Options;
using Pwneu.Api.Services;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Auths;

public static class Register
{
    public record Command(
        string UserName,
        string Email,
        string Password,
        string FullName,
        string AccessKey,
        string? TurnstileToken,
        string? IpAddress = null
    ) : IRequest<Result>;

    private static readonly Error Failed = new("Register.Failed", "Unable to create user");
    private static readonly Error InvalidAccessKey = new(
        "Register.InvalidAccessKey",
        "Invalid access key"
    );

    private static readonly Error EmailBlacklisted = new(
        "Register.EmailBlacklisted",
        "The specified email is not allowed to be used. Please use a different email"
    );

    private static readonly Error AddRoleFailed = new(
        "Register.AddRoleFailed",
        "Unable to add role to user. Please contact the administrator"
    );

    private static readonly Error EmailInUse = new(
        "Register.EmailInUse",
        "Email is already in use"
    );
    private static readonly Error UserNameInUse = new(
        "Register.UserNameInUse",
        "Username is already in use"
    );
    private static readonly Error InvalidUserName = new(
        "Register.InvalidUserName",
        "Invalid UserName"
    );

    private static readonly Error InvalidAntiSpamToken = new(
        "Register.InvalidAntiSpamToken",
        "Verification failed. Rejecting registration"
    );

    internal sealed class Handler(
        AppDbContext context,
        UserManager<User> userManager,
        ITurnstileValidator turnstileValidator,
        IFusionCache cache,
        IMediator mediator,
        IValidator<Command> validator,
        IOptions<AppOptions> appOptions,
        ILogger<Handler> logger
    ) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure(
                    new Error("Register.Validation", validationResult.ToString())
                );

            // Check if email domain is allowed.
            var validDomain = appOptions.Value.ValidEmailDomain;

            var isValidDomain = IsValidDomain(request.Email, validDomain);

            if (!isValidDomain)
                return Result.Failure(
                    new Error(
                        "Register.InvalidDomain",
                        $"The email domain '{request.Email.Split('@').LastOrDefault()}' is not allowed. Use the domain '{validDomain}'."
                    )
                );

            // Validate Turnstile from Cloudflare.
            var isValidTurnstile = await turnstileValidator.IsValidTurnstileTokenAsync(
                request.TurnstileToken,
                cancellationToken
            );

            if (!isValidTurnstile)
                return Result.Failure(InvalidAntiSpamToken);

            var blacklistedEmails = await cache.GetOrSetAsync(
                CacheKeys.BlacklistedEmails(),
                async _ => await context.BlacklistedEmails.ToListAsync(cancellationToken),
                token: cancellationToken
            );

            var blacklistedEmailsNoId = blacklistedEmails.Select(b => b.Email).ToList();

            if (blacklistedEmailsNoId.Contains(request.Email))
                return Result.Failure(EmailBlacklisted);

            // Set values of the variables above.
            var accessKeys = await cache.GetOrSetAsync(
                CacheKeys.AccessKeys(),
                async _ => await context.AccessKeys.ToListAsync(cancellationToken),
                token: cancellationToken
            );

            var accessKey = accessKeys.FirstOrDefault(a =>
                a.Id.ToString() == request.AccessKey && a.Expiration > DateTime.UtcNow
            );

            // If no access key matched, don't register the user.
            if (accessKey is null)
                return Result.Failure(InvalidAccessKey);

            var user = new User
            {
                UserName = request.UserName,
                Email = request.Email,
                FullName = request.FullName,
                IsVisibleOnLeaderboards = !accessKey.ForManager,
                CreatedAt = DateTime.UtcNow,
                RegistrationIpAddress = request.IpAddress,
            };

            var createUser = await userManager.CreateAsync(user, request.Password);
            if (!createUser.Succeeded)
            {
                var error = createUser.Errors.FirstOrDefault();
                return Result.Failure(
                    error?.Code switch
                    {
                        "DuplicateEmail" => EmailInUse,
                        "InvalidUserName" => InvalidUserName,
                        "DuplicateUserName" => UserNameInUse,
                        _ => Failed,
                    }
                );
            }

            IdentityResult addRole;

            if (accessKey.ForManager)
                addRole = await userManager.AddToRoleAsync(user, Roles.Manager);
            else
                addRole = await userManager.AddToRoleAsync(user, Roles.Member);

            if (!addRole.Succeeded)
            {
                // If role assignment fails, delete the user.
                await userManager.DeleteAsync(user);
                return Result.Failure(AddRoleFailed);
            }

            var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);

            // Uncomment if testing registration email.
            // await userManager.DeleteAsync(user);

            await mediator.Publish(
                new RegisteredEvent
                {
                    UserName = request.UserName,
                    Email = request.Email,
                    FullName = request.FullName,
                    ConfirmationToken = confirmationToken,
                    IpAddress = request.IpAddress,
                },
                cancellationToken
            );

            if (accessKey.CanBeReused)
                return Result.Success();

            // Remove key since it cannot be reused.
            context.AccessKeys.Remove(accessKey);
            await context.SaveChangesAsync(cancellationToken);

            // Update cache by removing the used key in the key list.
            await cache.SetAsync(
                CacheKeys.AccessKeys(),
                accessKeys.Where(a => a.Id != accessKey.Id).ToList(),
                token: cancellationToken
            );

            logger.LogInformation(
                "User registered: {UserName}, Email: {Email}, IP Address: {IpAddress}",
                request.UserName,
                request.Email,
                request.IpAddress
            );

            return Result.Success();

            static bool IsValidDomain(string email, string? validDomain)
            {
                if (string.IsNullOrWhiteSpace(validDomain))
                    return true;

                var emailDomain = email.Split('@').LastOrDefault();
                return emailDomain?.Equals(validDomain, StringComparison.OrdinalIgnoreCase) == true;
            }
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "identity/register",
                    async (RegisterRequest request, HttpContext httpContext, ISender sender) =>
                    {
                        var ipAddress = httpContext
                            .Request.Headers[CommonConstants.CfConnectingIp]
                            .ToString();

                        var command = new Command(
                            UserName: request.UserName,
                            Email: request.Email,
                            Password: request.Password,
                            FullName: request.FullName,
                            AccessKey: request.AccessKey,
                            TurnstileToken: request.TurnstileToken,
                            IpAddress: ipAddress
                        );

                        var result = await sender.Send(command);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.Created();
                    }
                )
                .RequireRateLimiting(RateLimitingPolicies.Registration)
                .WithTags(nameof(Auths));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Email)
                .NotEmpty()
                .WithMessage("Email is required.")
                .EmailAddress()
                .WithMessage("Email must be a valid email address.");

            RuleFor(c => c.UserName)
                .NotEmpty()
                .WithMessage("Username is required.")
                .MinimumLength(5)
                .WithMessage("Username must be at least 5 characters long.")
                .MaximumLength(40)
                .WithMessage("Username must be 40 characters or less.")
                .Matches(@"^[a-zA-Z0-9]+$")
                .WithMessage("Username can only contain letters and numbers.");

            RuleFor(c => c.FullName)
                .NotEmpty()
                .WithMessage("Fullname is required.")
                .MaximumLength(40)
                .WithMessage("Fullname must be 40 characters or less.")
                .Matches(@"^[a-zA-Z\s]+$")
                .WithMessage("Fullname must only contain letters and spaces.");

            RuleFor(c => c.AccessKey).NotEmpty().WithMessage("Access key is required.");

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
