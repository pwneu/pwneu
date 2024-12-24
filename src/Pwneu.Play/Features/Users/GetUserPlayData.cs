using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;

namespace Pwneu.Play.Features.Users;

public static class GetUserPlayData
{
    private static readonly Error NotFound = new(
        "GetUserPlayData.NotFound",
        "The user with the specified ID was not found");

    public record Query(string Id) : IRequest<Result<UserPlayDataResponse>>;

    internal sealed class Handler(ApplicationDbContext context, IMemberAccess memberAccess)
        : IRequestHandler<Query, Result<UserPlayDataResponse>>
    {
        public async Task<Result<UserPlayDataResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            // Check if user exists.
            if (!await memberAccess.MemberExistsAsync(request.Id, cancellationToken))
                return Result.Failure<UserPlayDataResponse>(NotFound);

            var totalSolves = await context
                .Solves
                .Where(s => s.UserId == request.Id)
                .CountAsync(cancellationToken);

            var totalHintUsages = await context
                .HintUsages
                .Where(s => s.UserId == request.Id)
                .CountAsync(cancellationToken);

            var userPlayData = new UserPlayDataResponse
            {
                Id = request.Id,
                TotalSolves = totalSolves,
                TotalHintUsages = totalHintUsages
            };

            return userPlayData;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/{id:Guid}/data", async (Guid id, ISender sender) =>
                {
                    var query = new Query(id.ToString());
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Users));

            app.MapGet("me/data", async (ClaimsPrincipal claims, ISender sender) =>
                {
                    var id = claims.GetLoggedInUserId<string>();
                    if (id is null) return Results.BadRequest();

                    var query = new Query(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .RequireRateLimiting(Consts.Fixed)
                .WithTags(nameof(Users));
        }
    }
}