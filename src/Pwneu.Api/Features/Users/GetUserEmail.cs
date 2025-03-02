using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions;
using System.Security.Claims;

namespace Pwneu.Api.Features.Users;

public static class GetUserEmail
{
    public record Query(string Id) : IRequest<Result<string>>;

    private static readonly Error NotFound = new("GetUserEmail.NotFound", "User not found");

    internal sealed class Handler(AppDbContext context) : IRequestHandler<Query, Result<string>>
    {
        public async Task<Result<string>> Handle(Query request, CancellationToken cancellationToken)
        {
            var user = await context
                .Users.Where(u => u.Id == request.Id)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(cancellationToken);

            return user ?? Result.Failure<string>(NotFound);
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "identity/users/{id:Guid}/email",
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
                    "identity/me/email",
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
