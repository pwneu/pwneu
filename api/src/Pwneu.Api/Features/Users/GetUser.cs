using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;

namespace Pwneu.Api.Features.Users;

public static class GetUser
{
    public record Response(string Id, string? Email);

    public record Query(Guid Id) : IRequest<Result<Response>>;

    internal sealed class Handler(ApplicationDbContext context) : IRequestHandler<Query, Result<Response>>
    {
        public async Task<Result<Response>> Handle(Query request, CancellationToken cancellationToken)
        {
            var user = await context
                .Users
                .Where(u => u.Id == request.Id.ToString())
                .Select(u => new Response(u.Id, u.Email))
                .FirstOrDefaultAsync(cancellationToken);

            if (user is null)
                return Result.Failure<Response>(new Error("GetUser.Null",
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
                .WithTags(nameof(ApplicationUser));
        }
    }
}