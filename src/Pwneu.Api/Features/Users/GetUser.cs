using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Users;

/// <summary>
/// Retrieves a user by ID, excluding those with a role of manager or admin.
/// Only users with manager or admin roles can access this endpoint.
/// </summary>
public static class GetUser
{
    public record Query(string Id) : IRequest<Result<UserDetailsResponse>>;

    private static readonly Error NotFound = new("GetUser.NotFound", "User not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, IAccessControl accessControl)
        : IRequestHandler<Query, Result<UserDetailsResponse>>
    {
        public async Task<Result<UserDetailsResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var managerIds = await accessControl.GetManagerIdsAsync(cancellationToken);

            if (managerIds.Contains(request.Id))
                return Result.Failure<UserDetailsResponse>(NotFound);

            // TODO -- Check for bugs in cache invalidations
            var user = await cache.GetOrSetAsync(Keys.User(request.Id), async _ =>
                await context
                    .Users
                    .Where(u => u.Id == request.Id)
                    .Select(u => new UserDetailsResponse(u.Id, u.UserName, u.Email, u.FullName, u.CreatedAt,
                        u.Solves.Sum(s => s.Challenge.Points),
                        u.FlagSubmissions.Count(fs => fs.FlagStatus == FlagStatus.Correct),
                        u.FlagSubmissions.Count(fs => fs.FlagStatus == FlagStatus.Incorrect)))
                    .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

            return user ?? Result.Failure<UserDetailsResponse>(NotFound);
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/{id:Guid}", async (Guid id, ISender sender) =>
                {
                    var query = new Query(id.ToString());
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Users));
        }
    }
}