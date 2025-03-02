using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.AccessKeys;

public static class DeleteAccessKey
{
    private static readonly Error NotFound = new(
        "DeleteAccessKey.NotFound",
        "The access key with the specified ID was not found"
    );

    public record Command(Guid Id) : IRequest<Result>;

    internal sealed class Handler(AppDbContext context, IFusionCache cache, ILogger<Handler> logger)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var accessKey = await context
                .AccessKeys.Where(a => a.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (accessKey is null)
                return Result.Failure(NotFound);

            context.AccessKeys.Remove(accessKey);

            await context.SaveChangesAsync(cancellationToken);

            await cache.RemoveAsync(CacheKeys.AccessKeys(), token: cancellationToken);

            logger.LogInformation("Access Key deleted: {Id}", request.Id);

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete(
                    "identity/keys/{id:Guid}",
                    async (Guid id, ISender sender) =>
                    {
                        var query = new Command(id);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.NoContent();
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .WithTags(nameof(AccessKeys));
        }
    }
}
