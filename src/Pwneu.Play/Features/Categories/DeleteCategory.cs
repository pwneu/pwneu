using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Categories;

/// <summary>
/// Deletes a category by ID.
/// Only the admin can delete a category.
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
                .Where(c => c.Id == request.Id)
                .Include(c => c.Challenges)
                .ThenInclude(ch => ch.Artifacts)
                .Include(c => c.Challenges)
                .ThenInclude(ch => ch.Hints)
                .FirstOrDefaultAsync(cancellationToken);

            if (category is null) return Result.Failure(NotFound);

            context.Categories.Remove(category);

            await context.SaveChangesAsync(cancellationToken);

            var invalidationTasks = new List<Task>
            {
                cache.InvalidateCategoryCacheAsync(category.Id, cancellationToken),
                cache.InvalidateUserGraphs(cancellationToken),
                cache.RemoveAsync(Keys.UserRanks(), token: cancellationToken).AsTask(),
                cache.RemoveAsync(Keys.TopUsersGraph(), token: cancellationToken).AsTask(),
                cache.RemoveAsync(Keys.AllChallenges(), token: cancellationToken).AsTask()
            };

            invalidationTasks.AddRange(category.Challenges.Select(challenge =>
                cache.InvalidateChallengeCacheAsync(challenge, cancellationToken)));

            await Task.WhenAll(invalidationTasks);

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
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(Categories));
        }
    }
}