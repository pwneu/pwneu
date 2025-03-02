using FluentValidation;
using MediatR;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.BlacklistedEmails;

public static class AddEmailToBlacklist
{
    public record Command(string Email) : IRequest<Result>;

    internal sealed class Handler(
        AppDbContext context,
        IFusionCache cache,
        IValidator<Command> validator,
        ILogger<Handler> logger
    ) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure(
                    new Error("AddBlacklistedEmail.Validation", validationResult.ToString())
                );

            var blacklistedEmail = BlacklistedEmail.Create(request.Email);

            context.Add(blacklistedEmail);

            await context.SaveChangesAsync(cancellationToken);

            await cache.RemoveAsync(CacheKeys.BlacklistedEmails(), token: cancellationToken);

            logger.LogInformation("Email added on blacklist: {Email}", request.Email);

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "identity/blacklist",
                    async (AddEmailToBlacklistRequest request, ISender sender) =>
                    {
                        var command = new Command(request.Email);

                        var result = await sender.Send(command);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.NoContent();
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .WithTags(nameof(BlacklistedEmails));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Email)
                .EmailAddress()
                .WithMessage("Email must be a valid email address.");
        }
    }
}
