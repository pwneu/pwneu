using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Submissions;

public static class HideUserOnLeaderboards
{
    public record Command(string Id) : IRequest<Result>;

    private static readonly Error NotFound = new(
        "HideUserOnLeaderboards.NotFound",
        "The user with the specified ID was not found"
    );

    internal sealed class Handler(AppDbContext context, IFusionCache cache)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var userExists = await cache.CheckIfUserExistsAsync(
                context,
                request.Id,
                cancellationToken
            );
            if (!userExists)
                return Result.Failure(NotFound);

            await context
                .Users.Where(u => u.Id == request.Id)
                .ExecuteUpdateAsync(
                    u => u.SetProperty(u => u.IsVisibleOnLeaderboards, false),
                    cancellationToken
                );

            var invalidationTasks = new List<Task>
            {
                cache
                    .RemoveAsync(CacheKeys.UserDetailsNoEmail(request.Id), token: cancellationToken)
                    .AsTask(),
                cache.RemoveAsync(CacheKeys.UserRank(request.Id), token: cancellationToken).AsTask(),
                cache.RemoveAsync(CacheKeys.UserRanks(), token: cancellationToken).AsTask(),
                cache.RemoveAsync(CacheKeys.TopUsersGraph(), token: cancellationToken).AsTask(),
            };

            await Task.WhenAll(invalidationTasks);

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPut(
                    "play/users/{id}/hide",
                    async (string id, ISender sender) =>
                    {
                        var query = new Command(id);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.NoContent();
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .WithTags(nameof(Submissions));
        }
    }
}
