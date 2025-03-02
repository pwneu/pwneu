using FluentValidation;
using MediatR;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.AccessKeys;

public static class CreateAccessKey
{
    public record Command(bool ForManager, bool CanBeReused, DateTime Expiration)
        : IRequest<Result<Guid>>;

    internal sealed class Handler(
        AppDbContext context,
        IFusionCache cache,
        IValidator<Command> validator,
        ILogger<Handler> logger
    ) : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(
                    new Error("CreateAccessKey.Validation", validationResult.ToString())
                );

            var accessKey = AccessKey.Create(
                request.ForManager,
                request.CanBeReused,
                request.Expiration
            );

            context.Add(accessKey);

            await context.SaveChangesAsync(cancellationToken);

            await cache.RemoveAsync(CacheKeys.AccessKeys(), token: cancellationToken);

            logger.LogInformation("Access Key created: {Id}", accessKey.Id);

            return accessKey.Id;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "identity/keys",
                    async (CreateAccessKeyRequest request, ISender sender) =>
                    {
                        var command = new Command(
                            request.ForManager,
                            request.CanBeReused,
                            request.Expiration
                        );

                        var result = await sender.Send(command);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .WithTags(nameof(AccessKeys));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Expiration)
                .GreaterThan(DateTime.UtcNow)
                .WithMessage("Expiration date must be in the future.");
        }
    }
}
