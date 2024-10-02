using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Hints;

public static class RemoveHint
{
    public record Command(Guid Id) : IRequest<Result>;

    private static readonly Error NotFound = new("DeleteHint.NotFound",
        "The hint with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var hint = await context
                .Hints
                .Where(h => h.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (hint is null)
                return Result.Failure(NotFound);

            // Get hint's category id first before deleting the hint.
            var hintCategoryId = await context
                .Hints
                .Where(h => h.Id == request.Id)
                .Select(h => h.Challenge.CategoryId)
                .FirstOrDefaultAsync(cancellationToken);

            context.Remove(hint);

            await context.SaveChangesAsync(cancellationToken);

            var invalidationTasks = new List<Task>
            {
                cache.InvalidateUserGraphs(cancellationToken),
                cache.InvalidateCategoryCacheAsync(hintCategoryId, cancellationToken),
                cache.RemoveAsync(Keys.UserRanks(), token: cancellationToken).AsTask(),
                cache.RemoveAsync(Keys.TopUsersGraph(), token: cancellationToken).AsTask(),
            };

            await Task.WhenAll(invalidationTasks);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("hints/{id:Guid}", async (Guid id, ISender sender) =>
                {
                    var query = new Command(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Hints));
        }
    }
}