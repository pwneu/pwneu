using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.Users;

/// <summary>
/// Retrieves user details by ID, excluding those with a role of manager or admin.
/// Only users with manager or admin roles can access this endpoint.
/// </summary>
public static class GetUserDetails
{
    public record Query(string Id) : IRequest<Result<UserDetailsResponse>>;

    private static readonly Error NotFound = new("GetUserDetails.NotFound", "User not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<UserDetailsResponse>>
    {
        public async Task<Result<UserDetailsResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var user = await cache.GetOrSetAsync(Keys.UserDetails(request.Id), async _ =>
                await context
                    .Users
                    .Where(u => u.Id == request.Id)
                    .Select(u => new UserDetailsResponse
                    {
                        Id = u.Id,
                        UserName = u.UserName,
                        FullName = u.FullName,
                        CreatedAt = u.CreatedAt,
                    })
                    .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

            return user ?? Result.Failure<UserDetailsResponse>(NotFound);
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/{id:Guid}/details", async (Guid id, ISender sender) =>
                {
                    var query = new Query(id.ToString());
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .WithTags(nameof(Users));

            app.MapGet("me/details", async (ClaimsPrincipal claims, ISender sender) =>
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