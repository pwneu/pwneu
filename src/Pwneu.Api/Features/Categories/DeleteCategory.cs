using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Extensions.Entities;
using Pwneu.Api.Services;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Categories;

public static class DeleteCategory
{
    public record Command(Guid Id, string UserId, string UserName) : IRequest<Result>;

    private static readonly Error NotFound = new(
        "DeleteCategory.NotFound",
        "The category with the specified ID was not found"
    );

    private static readonly Error NotAllowed = new(
        "DeleteCategory.NotAllowed",
        "Not allowed to delete categories when submissions are enabled"
    );

    internal sealed class Handler(AppDbContext context, IFusionCache cache, ILogger<Handler> logger)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var submissionsEnabled = await cache.CheckIfSubmissionsAllowedAsync(
                context,
                cancellationToken
            );

            if (submissionsEnabled)
                return Result.Failure(NotAllowed);

            var category = await context
                .Categories.Where(c => c.Id == request.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (category is null)
                return Result.Failure(NotFound);

            context.Categories.Remove(category);

            var audit = Audit.Create(
                request.UserId,
                request.UserName,
                $"Category {request.Id} deleted"
            );

            context.Add(audit);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Category ({Id}) deleted by {UserName} ({UserId})",
                request.Id,
                request.UserName,
                request.UserId
            );

            var invalidationTasks = new List<Task>
            {
                cache.RemoveAsync(CacheKeys.Categories(), token: cancellationToken).AsTask(),
            };

            await Task.WhenAll(invalidationTasks);

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete(
                    "play/categories/{id:Guid}",
                    async (
                        Guid id,
                        ClaimsPrincipal claims,
                        ISender sender,
                        IChallengePointsConcurrencyGuard guard
                    ) =>
                    {
                        if (!await guard.TryEnterAsync())
                            return Results.BadRequest(Error.AnotherProcessRunning);

                        try
                        {
                            var userId = claims.GetLoggedInUserId<string>();
                            if (userId is null)
                                return Results.BadRequest();

                            var userName = claims.GetLoggedInUserName();
                            if (userName is null)
                                return Results.BadRequest();

                            var query = new Command(id, userId, userName);
                            var result = await sender.Send(query);

                            return result.IsFailure
                                ? Results.BadRequest(result.Error)
                                : Results.NoContent();
                        }
                        finally
                        {
                            guard.Exit();
                        }
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .WithTags(nameof(Categories));
        }
    }
}
