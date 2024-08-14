using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Hints;

public static class GetChallengeHints
{
    public record Query(Guid ChallengeId) : IRequest<Result<IEnumerable<HintDetailsResponse>>>;

    private static readonly Error ChallengeNotFound = new("GetHints.ChallengeNotFound",
        "The challenge with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<IEnumerable<HintDetailsResponse>>>
    {
        public async Task<Result<IEnumerable<HintDetailsResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var hints = await cache.GetOrSetAsync(Keys.Hints(request.ChallengeId), async _ =>
                await context
                    .Challenges
                    .Where(c => c.Id == request.ChallengeId)
                    .Select(c => c.Hints
                        .Select(h => new HintDetailsResponse
                        {
                            Id = h.Id,
                            Content = h.Content,
                            Deduction = h.Deduction
                        })
                        .ToList())
                    .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

            return hints ?? Result.Failure<IEnumerable<HintDetailsResponse>>(ChallengeNotFound);
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("challenges/{id:Guid}/hints", async (Guid id, ISender sender) =>
                {
                    var query = new Query(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Hints));
        }
    }
}