using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Categories;

/// <summary>
/// Deletes a category by ID.
/// Only users with manager or admin roles can access this endpoint.
/// </summary>
public static class DeleteCategory
{
    public record Command(Guid Id) : IRequest<Result>;

    private static readonly Error NotFound = new("DeleteCategory.NotFound",
        "The category with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var category = await context
                .Categories
                .Where(ctg => ctg.Id == request.Id)
                .Include(ctg => ctg.Challenges)
                .ThenInclude(c => c.Artifacts)
                .FirstOrDefaultAsync(cancellationToken);

            if (category is null) return Result.Failure(NotFound);

            context.Categories.Remove(category);

            await context.SaveChangesAsync(cancellationToken);

            await cache.RemoveAsync(Keys.Categories(), token: cancellationToken);
            await cache.RemoveAsync(Keys.Category(category.Id), token: cancellationToken);

            foreach (var challenge in category.Challenges)
            {
                foreach (var file in challenge.Artifacts)
                    await cache.RemoveAsync(Keys.Artifact(file.Id), token: cancellationToken);

                await cache.RemoveAsync(Keys.Challenge(challenge.Id), token: cancellationToken);
                await cache.RemoveAsync(Keys.Flags(challenge.Id), token: cancellationToken);
            }

            // TODO -- Update cache on user evaluations

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("categories/{id:Guid}", async (Guid id, ISender sender) =>
                {
                    var query = new Command(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Categories));
        }
    }
}