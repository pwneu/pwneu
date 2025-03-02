using MediatR;
using Microsoft.AspNetCore.Identity;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Extensions.Entities;
using System.Security.Claims;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Users;

public static class GetUserDetails
{
    public record Query(string Id) : IRequest<Result<UserDetailsNoEmailResponse>>;

    private static readonly Error NotFound = new("GetUserDetails.NotFound", "User not found");

    internal sealed class Handler(
        AppDbContext context,
        UserManager<User> userManager,
        IFusionCache cache
    ) : IRequestHandler<Query, Result<UserDetailsNoEmailResponse>>
    {
        public async Task<Result<UserDetailsNoEmailResponse>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var user = await cache.GetUserDetailsNoEmailAsync(
                context,
                userManager,
                request.Id,
                cancellationToken
            );

            return user ?? Result.Failure<UserDetailsNoEmailResponse>(NotFound);
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "identity/users/{id:Guid}/details",
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
                .WithTags(nameof(Users));

            app.MapGet(
                    "identity/me/details",
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
                .WithTags(nameof(Users));
        }
    }
}
