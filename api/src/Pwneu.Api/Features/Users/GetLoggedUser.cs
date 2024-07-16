using MediatR;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Entities;
using Pwneu.Api.Shared.Extensions;

namespace Pwneu.Api.Features.Users;

public static class GetLoggedUser
{
    public record Response(string Id, string? Email);
    public record Query : IRequest<Result<Response>>;

    internal sealed class Handler(IHttpContextAccessor httpContextAccessor) : IRequestHandler<Query, Result<Response>>
    {
        public Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var userEmail = httpContextAccessor.HttpContext?.User.GetLoggedInUserEmail();
            var userId = httpContextAccessor.HttpContext?.User.GetLoggedInUserId<string>();

            return Task.FromResult(string.IsNullOrEmpty(userId)
                ? Result.Failure<Response>(new Error("GetLoggedUser.NoId", "No Id found"))
                : new Response(userId, userEmail));
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/me", async (ISender sender) =>
                {
                    var query = new Query();
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .WithTags(nameof(ApplicationUser));
        }
    }
}