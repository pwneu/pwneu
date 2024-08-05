using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.AccessKeys;

public static class DeleteAccessKey
{
    private static readonly Error NotFound = new("DeleteAccessKey.NotFound",
        "The access key with the specified ID was not found");

    public record Command(Guid Id) : IRequest<Result>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var accessKey = await context
                .AccessKeys
                .Where(a => a.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (accessKey is null) return Result.Failure(NotFound);

            context.AccessKeys.Remove(accessKey);

            await context.SaveChangesAsync(cancellationToken);

            await cache.RemoveAsync(Keys.AccessKeys(), token: cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("keys/{id:Guid}", async (Guid id, ISender sender) =>
                {
                    var query = new Command(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(AccessKeys));
        }
    }
}