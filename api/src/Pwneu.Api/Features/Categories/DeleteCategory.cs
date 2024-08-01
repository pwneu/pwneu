using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Categories;

/// <summary>
/// Deletes a category by ID.
/// Only users with manager or admin roles can access this endpoint.
/// </summary>
public static class DeleteCategory
{
    private static readonly Error NotFound = new("DeleteCategory.NotFound",
        "The category with the specified ID was not found");

    public record Command(Guid Id) : IRequest<Result>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var category = await context
                .Categories
                .Where(ctg => ctg.Id == request.Id)
                .Include(ctg => ctg.Challenges)
                .ThenInclude(c => c.ChallengeFiles)
                .FirstOrDefaultAsync(cancellationToken);

            if (category is null) return Result.Failure(NotFound);

            context.Categories.Remove(category);

            await context.SaveChangesAsync(cancellationToken);

            await cache.RemoveAsync($"{nameof(CategoryResponse)}:{category.Id}", token: cancellationToken);

            foreach (var challenge in category.Challenges)
            {
                foreach (var file in challenge.ChallengeFiles)
                    await cache.RemoveAsync($"{nameof(ChallengeFile)}:{file.Id}", token: cancellationToken);

                await cache.RemoveAsync($"{nameof(ChallengeDetailsResponse)}:{challenge.Id}", token: cancellationToken);
                await cache.RemoveAsync($"{nameof(Challenge)}.{nameof(Challenge.Flags)}:{challenge.Id}",
                    token: cancellationToken);
            }

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