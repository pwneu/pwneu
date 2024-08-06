using MediatR;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;

namespace Pwneu.Api.Features.Auths;

public static class GetLoggedUser
{
    public record Query : IRequest<Result<UserResponse>>;

    internal sealed class Handler(IHttpContextAccessor httpContextAccessor)
        : IRequestHandler<Query, Result<UserResponse>>
    {
        public Task<Result<UserResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var userName = httpContextAccessor.HttpContext?.User.GetLoggedInUserName();
            var userId = httpContextAccessor.HttpContext?.User.GetLoggedInUserId<string>();

            return Task.FromResult(string.IsNullOrEmpty(userId)
                ? Result.Failure<UserResponse>(new Error("GetLoggedUser.NoId", "No Id found"))
                : new UserResponse(userId, userName));
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("me", async (ISender sender) =>
                {
                    var query = new Query();
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .WithTags(nameof(Auths));
        }
    }
}