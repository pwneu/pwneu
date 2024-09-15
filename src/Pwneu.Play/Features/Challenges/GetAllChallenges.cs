using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Challenges;

public static class GetAllChallenges
{
    public record Query : IRequest<Result<IEnumerable<CategoryDetailsResponse>>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<IEnumerable<CategoryDetailsResponse>>>
    {
        public async Task<Result<IEnumerable<CategoryDetailsResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var categories = await cache.GetOrSetAsync(Keys.AllChallenges(), async _ =>
                await context
                    .Categories
                    .Select(c => new CategoryDetailsResponse
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Description = c.Description,
                        Challenges = c.Challenges.Select(ch => new ChallengeResponse
                        {
                            Id = ch.Id,
                            Name = ch.Name,
                            Description = ch.Description,
                            Points = ch.Points,
                            DeadlineEnabled = ch.DeadlineEnabled,
                            Deadline = ch.Deadline,
                            SolveCount = ch.SolveCount
                        }).ToList()
                    })
                    .ToListAsync(cancellationToken), token: cancellationToken);

            return categories;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("challenges/all", async (ISender sender) =>
                {
                    var query = new Query();
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.StatusCode(500) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .WithTags(nameof(Categories));
        }
    }
}