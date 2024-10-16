using System.Security.Claims;
using MediatR;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Users;

public static class GetUserGraph
{
    public record Query(string Id) : IRequest<Result<IEnumerable<UserActivityResponse>>>;

    private static readonly Error NotFound = new("GetUserGraph.NotFound",
        "The user with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, IMemberAccess memberAccess)
        : IRequestHandler<Query, Result<IEnumerable<UserActivityResponse>>>
    {
        public async Task<Result<IEnumerable<UserActivityResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            // Check if user exists.
            if (!await memberAccess.MemberExistsAsync(request.Id, cancellationToken))
                return Result.Failure<IEnumerable<UserActivityResponse>>(NotFound);

            var userGraph = await cache.GetOrSetAsync(Keys.UserGraph(request.Id),
                async _ =>
                {
                    var userGraph = await context.GetUserGraphAsync(request.Id, cancellationToken);
                    return userGraph;
                },
                token: cancellationToken);

            var activeUserIds = await cache.GetOrDefaultAsync<List<string>>(
                Keys.ActiveUserIds(),
                token: cancellationToken) ?? [];

            if (!activeUserIds.Contains(request.Id))
                activeUserIds.Add(request.Id);

            // Store the userId to the cache for easier invalidations.
            await cache.SetAsync(
                Keys.ActiveUserIds(),
                activeUserIds, token: cancellationToken);

            return userGraph;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/{id:Guid}/graph", async (Guid id, ISender sender) =>
                {
                    var query = new Query(id.ToString());
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Users));

            app.MapGet("me/graph", async (ClaimsPrincipal claims, ISender sender) =>
                {
                    var id = claims.GetLoggedInUserId<string>();
                    if (id is null) return Results.BadRequest();

                    var query = new Query(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .WithTags(nameof(Users));
        }
    }
}