using FluentValidation;
using MediatR;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.AccessKeys;

public static class CreateAccessKey
{
    public record Command(string Key, bool CanBeReused, DateTime Expiration) : IRequest<Result>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, IValidator<Command> validator)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure(new Error("CreateAccessKey.Validation", validationResult.ToString()));

            var category = new AccessKey
            {
                Id = Guid.NewGuid(),
                Key = request.Key,
                CanBeReused = request.CanBeReused,
                Expiration = request.Expiration.ToUniversalTime()
            };

            context.Add(category);

            await context.SaveChangesAsync(cancellationToken);

            await cache.RemoveAsync(Keys.AccessKeys(), token: cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("keys", async (CreateAccessKeyRequest request, ISender sender) =>
                {
                    var command = new Command(request.Key, request.CanBeReused, request.Expiration);

                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(AccessKeys));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Key)
                .NotEmpty()
                .WithMessage("Key is required.")
                .MaximumLength(100)
                .WithMessage("Key must be 100 characters or less.");
        }
    }
}