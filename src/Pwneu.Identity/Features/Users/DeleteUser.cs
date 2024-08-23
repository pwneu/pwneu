using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.Users;

public static class DeleteUser
{
    public record Command(string Id) : IRequest<Result>;

    private static readonly Error NotFound = new("DeleteUser.NotFound",
        "The user with the specified ID was not found");

    internal sealed class Handler(
        UserManager<User> userManager,
        IFusionCache cache,
        IPublishEndpoint endpoint) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var user = await userManager.FindByIdAsync(request.Id);

            if (user is null)
                return Result.Failure(NotFound);

            await userManager.DeleteAsync(user);

            var invalidationTasks = new List<Task>
            {
                cache.RemoveAsync(Keys.User(request.Id), token: cancellationToken).AsTask(),
                cache.RemoveAsync(Keys.UserDetails(request.Id), token: cancellationToken).AsTask(),
            };

            await Task.WhenAll(invalidationTasks);

            await endpoint.Publish(new UserDeletedEvent { Id = request.Id }, cancellationToken);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete("users/{id}", async (string id, ISender sender) =>
                {
                    var query = new Command(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(Users));
        }
    }
}