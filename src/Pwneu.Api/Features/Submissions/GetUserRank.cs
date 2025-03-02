using MediatR;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions;
using Pwneu.Api.Extensions.Entities;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Submissions;

public static class GetUserRank
{
    public record Query(string Id) : IRequest<Result<UserRankResponse?>>;

    internal sealed class Handler(AppDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<UserRankResponse?>>
    {
        public async Task<Result<UserRankResponse?>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var publicLeaderboardCount = await cache.GetPublicLeaderboardCountAsync(
                context,
                cancellationToken
            );

            var userRanks = await cache.GetUserRanks(
                context,
                publicLeaderboardCount,
                cancellationToken
            );

            var userRank = userRanks.UserRanks.Where(ur => ur.Id == request.Id).FirstOrDefault();
            if (userRank is not null)
                return userRank;

            userRank = await cache.GetUserRankAsync(context, request.Id, 0, cancellationToken);

            return userRank;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/users/{id:Guid}/rank",
                    async (Guid id, ISender sender) =>
                    {
                        var query = new Query(id.ToString());
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.NotFound(result.Error)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .WithTags(nameof(Submissions));

            app.MapGet(
                    "play/me/rank",
                    async (ClaimsPrincipal claims, ISender sender) =>
                    {
                        var id = claims.GetLoggedInUserId<string>();
                        if (id is null)
                            return Results.BadRequest();

                        var query = new Query(id);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.NotFound(result.Error)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization()
                .WithTags(nameof(Submissions));
        }
    }
}
