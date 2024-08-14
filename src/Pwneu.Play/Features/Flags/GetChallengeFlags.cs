using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Flags;

/// <summary>
/// Retrieves a list of flags in a challenge.
/// Only users with manager or admin roles can access this endpoint.
/// </summary>
public static class GetChallengeFlags
{
    public record Query(Guid ChallengeId) : IRequest<Result<IEnumerable<string>>>;

    private static readonly Error ChallengeNotFound = new("GetChallengeFlags.ChallengeNotFound",
        "The challenge with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<IEnumerable<string>>>
    {
        public async Task<Result<IEnumerable<string>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var flags = await cache.GetOrSetAsync(Keys.Flags(request.ChallengeId), async _ =>
                await context
                    .Challenges
                    .Where(c => c.Id == request.ChallengeId)
                    .Select(c => c.Flags)
                    .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

            return flags ?? Result.Failure<IEnumerable<string>>(ChallengeNotFound);
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

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Flags));
        }
    }
}