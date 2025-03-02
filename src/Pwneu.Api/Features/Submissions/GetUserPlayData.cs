using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions;
using Pwneu.Api.Extensions.Entities;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Submissions;

public static class GetUserPlayData
{
    private static readonly Error NotFound = new(
        "GetUserPlayData.NotFound",
        "The user with the specified ID was not found"
    );

    public record Query(string Id) : IRequest<Result<UserPlayDataResponse>>;

    internal sealed class Handler(AppDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<UserPlayDataResponse>>
    {
        public async Task<Result<UserPlayDataResponse>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            // Check if user exists.
            var userExists = await cache.CheckIfUserExistsAsync(
                context,
                request.Id,
                cancellationToken
            );

            if (!userExists)
                return Result.Failure<UserPlayDataResponse>(NotFound);

            var totalSolves = await context
                .Solves.Where(s => s.UserId == request.Id)
                .CountAsync(cancellationToken);

            var totalHintUsages = await context
                .HintUsages.Where(s => s.UserId == request.Id)
                .CountAsync(cancellationToken);

            var userPlayData = new UserPlayDataResponse
            {
                Id = request.Id,
                TotalSolves = totalSolves,
                TotalHintUsages = totalHintUsages,
            };

            return userPlayData;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/users/{id:Guid}/data",
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
                    "play/me/data",
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
                .RequireRateLimiting(RateLimitingPolicies.Fixed)
                .WithTags(nameof(Submissions));
        }
    }
}
