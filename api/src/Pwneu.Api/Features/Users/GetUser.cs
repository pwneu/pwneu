using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Users;

public static class GetUser
{
    public record Query(Guid Id) : IRequest<Result<UserResponse>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<UserResponse>>
    {
        public async Task<Result<UserResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var user = await cache.GetOrSetAsync($"{nameof(User)}:{request.Id}", async _ =>
            {
                return await context
                    .Users
                    .Where(u => u.Id == request.Id.ToString())
                    .Select(u => new UserResponse(u.Id, u.UserName))
                    .FirstOrDefaultAsync(cancellationToken);
            }, token: cancellationToken);

            if (user is null)
                return Result.Failure<UserResponse>(new Error("GetUser.Null",
                    "The user with the specified ID was not found"));

            return user;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/{id:Guid}", async (Guid id, ISender sender) =>
                {
                    var query = new Query(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .WithTags(nameof(User));
        }
    }
}