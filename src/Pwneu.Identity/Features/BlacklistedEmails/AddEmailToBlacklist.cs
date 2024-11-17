using FluentValidation;
using MediatR;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.BlacklistedEmails;

public static class AddEmailToBlacklist
{
    public record Command(string Email) : IRequest<Result>;

    internal sealed class Handler(
        ApplicationDbContext context,
        IFusionCache cache,
        IValidator<Command> validator,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(new Error("AddBlacklistedEmail.Validation", validationResult.ToString()));

            var blacklistedEmail = new BlacklistedEmail
            {
                Id = Guid.NewGuid(),
                Email = request.Email
            };

            context.Add(blacklistedEmail);

            await context.SaveChangesAsync(cancellationToken);

            await cache.RemoveAsync(Keys.BlacklistedEmails(), token: cancellationToken);

            logger.LogInformation("Email added on blacklist: {Email}", request.Email);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("blacklist", async (AddEmailToBlacklistRequest request, ISender sender) =>
                {
                    var command = new Command(request.Email);

                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Consts.AdminOnly)
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