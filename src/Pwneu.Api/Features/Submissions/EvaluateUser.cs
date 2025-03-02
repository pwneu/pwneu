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

public static class EvaluateUser
{
    public record Query(string Id) : IRequest<Result<UserEvaluationResponse>>;

    private static readonly Error NotFound = new(
        "EvaluateUser.NotFound",
        "The user with the specified ID was not found"
    );

    internal sealed class Handler(AppDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<UserEvaluationResponse>>
    {
        public async Task<Result<UserEvaluationResponse>> Handle(
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
                return Result.Failure<UserEvaluationResponse>(NotFound);

            var categoryEvaluations = await cache.GetUserEvaluationsAsync(
                context,
                request.Id,
                cancellationToken
            );

            return new UserEvaluationResponse
            {
                Id = request.Id,
                CategoryEvaluations = categoryEvaluations,
            };
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/users/{id:Guid}/evaluate",
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
                    "play/me/evaluate",
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
                .RequireRateLimiting(RateLimitingPolicies.ExpensiveRequest)
                .WithTags(nameof(Submissions));
        }
    }
}
