using MediatR;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;

namespace Pwneu.Identity.Features.Auths;

public static class Me
{
    public record Query : IRequest<Result<UserInfoResponse>>;

    internal sealed class Handler(IHttpContextAccessor httpContextAccessor)
        : IRequestHandler<Query, Result<UserInfoResponse>>
    {
        public Task<Result<UserInfoResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var userName = httpContextAccessor.HttpContext?.User.GetLoggedInUserName();
            var userId = httpContextAccessor.HttpContext?.User.GetLoggedInUserId<string>();
            var userRoles = httpContextAccessor.HttpContext?.User.GetRoles().ToList();

            if (string.IsNullOrEmpty(userId))
                return Task.FromResult(Result.Failure<UserInfoResponse>(new Error("GetMe.NoId", "No Id found")));

            if (userRoles is null)
                return Task.FromResult(Result.Failure<UserInfoResponse>(new Error("GetMe.NoRoles", "No Roles found")));

            var response = new UserInfoResponse
            {
                Id = userId,
                UserName = userName,
                Roles = userRoles
            };

            return Task.FromResult(Result.Success(response));
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