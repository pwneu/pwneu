using MediatR;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.AccessKeys;

public static class CreateAccessKey
{
    public record Command(bool CanBeReused, DateTime Expiration) : IRequest<Result<Guid>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
        {
            var accessKey = new AccessKey
            {
                Id = Guid.NewGuid(),
                CanBeReused = request.CanBeReused,
                Expiration = request.Expiration
            };

            context.Add(accessKey);

            await context.SaveChangesAsync(cancellationToken);

            await cache.RemoveAsync(Keys.AccessKeys(), token: cancellationToken);

            return accessKey.Id;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("keys", async (CreateAccessKeyRequest request, ISender sender) =>
                {
                    var command = new Command(request.CanBeReused, request.Expiration);

                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(AccessKeys));
        }
    }
}