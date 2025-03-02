using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Submissions;

public static class ShowUserOnLeaderboards
{
    public record Command(string Id) : IRequest<Result>;

    private static readonly Error NotFound = new(
        "ShowUserOnLeaderboards.NotFound",
        "The user with the specified ID was not found"
    );

    private static readonly Error NotAllowed = new(
        "ShowUserOnLeaderboards.NotAllowed",
        "Managers are not allowed to play!"
    );

    internal sealed class Handler(
        UserManager<User> userManager,
        AppDbContext context,
        IFusionCache cache
    ) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var user = userManager.Users.SingleOrDefault(u => u.Id == request.Id);
            if (user is null)
                return Result.Failure(NotFound);

            var userIsManager = await userManager.IsInRoleAsync(user, Roles.Manager);
            if (userIsManager)
                return Result.Failure(NotAllowed);

            await context
                .Users.Where(u => u.Id == request.Id)
                .ExecuteUpdateAsync(
                    u => u.SetProperty(u => u.IsVisibleOnLeaderboards, true),
                    cancellationToken
                );

            var invalidationTasks = new List<Task>
            {
                cache
                    .RemoveAsync(CacheKeys.UserDetailsNoEmail(request.Id), token: cancellationToken)
                    .AsTask(),
                cache
                    .RemoveAsync(CacheKeys.UserRank(request.Id), token: cancellationToken)
                    .AsTask(),
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
                    "play/users/{id}/show",
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
