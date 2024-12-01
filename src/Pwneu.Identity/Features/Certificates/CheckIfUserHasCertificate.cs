using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;

namespace Pwneu.Identity.Features.Certificates;

public static class CheckIfUserHasCertificate
{
    public record Query(string UserId) : IRequest<Result<bool>>;

    public static readonly Error UserNotFound = new(
        "GetUserCertificate.UserNotFound",
        "The user with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, UserManager<User> userManager)
        : IRequestHandler<Query, Result<bool>>
    {
        public async Task<Result<bool>> Handle(Query request, CancellationToken cancellationToken)
        {
            var user = userManager.Users.SingleOrDefault(u => u.Id == request.UserId);

            if (user is null)
                return Result.Failure<bool>(UserNotFound);

            var hasCertificate = await context
                .Certificates
                .Where(c => c.UserId == request.UserId)
                .AnyAsync(cancellationToken);

            return hasCertificate;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/{id:Guid}/certificate/check", async (Guid id, ISender sender) =>
                {
                    var query = new Query(id.ToString());
                    var result = await sender.Send(query);

                    return result.IsFailure
                        ? Results.NotFound(result.Error)
                        : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Certificates));

            app.MapGet("me/certificate/check", async (ClaimsPrincipal claims, ISender sender) =>
                {
                    var id = claims.GetLoggedInUserId<string>();
                    if (id is null) return Results.BadRequest();

                    var command = new Query(id);
                    var result = await sender.Send(command);

                    return result.IsFailure
                        ? Results.NotFound(result.Error)
                        : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.MemberOnly)
                .RequireRateLimiting(Consts.Generate)
                .WithTags(nameof(Certificates));
        }
    }
}