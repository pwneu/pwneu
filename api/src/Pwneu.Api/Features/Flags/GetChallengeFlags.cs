using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.Features.Flags;

public static class GetChallengeFlags
{
    public record Query(Guid Id) : IRequest<Result<IEnumerable<string>>>;

    internal sealed class Handler(ApplicationDbContext context) : IRequestHandler<Query, Result<IEnumerable<string>>>
    {
        public async Task<Result<IEnumerable<string>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var challengeFlagsResponse = await context
                .Challenges
                .Where(c => c.Id == request.Id)
                .Select(c => c.Flags)
                .FirstOrDefaultAsync(cancellationToken);

            if (challengeFlagsResponse is null)
                return Result.Failure<IEnumerable<string>>(new Error("GetChallengeFlags.Null",
                    "The challenge with the specified ID was not found"));

            // This shouldn't happen since a challenge must require at least one flag
            if (challengeFlagsResponse.Count == 0)
                return Result.Failure<IEnumerable<string>>(new Error("GetChallengeFlags.NoFlags",
                    "The challenge with the specified ID doesn't have any flags"));

            return challengeFlagsResponse;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("challenges/{id:Guid}/flags", async (Guid id, ISender sender) =>
                {
                    var query = new Query(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound() : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .WithTags(nameof(Challenge));
        }
    }
}