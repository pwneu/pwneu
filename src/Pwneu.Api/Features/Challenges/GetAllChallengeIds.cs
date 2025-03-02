using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Data;

namespace Pwneu.Api.Features.Challenges;

public static class GetAllChallengeIds
{
    public record Query : IRequest<Result<List<Guid>>>;

    internal sealed class Handler(AppDbContext context) : IRequestHandler<Query, Result<List<Guid>>>
    {
        public async Task<Result<List<Guid>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var challengeIds = await context
                .Challenges.Select(ch => ch.Id)
                .ToListAsync(cancellationToken);

            return challengeIds;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/challenges/all",
                    async (ISender sender) =>
                    {
                        var query = new Query();
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.StatusCode(500)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization()
                .WithTags(nameof(Challenges));
        }
    }
}
