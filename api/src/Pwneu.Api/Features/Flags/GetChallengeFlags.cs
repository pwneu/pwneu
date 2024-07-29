using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Flags;

/// <summary>
/// Retrieves a list of flags in a challenge.
/// Only users with manager or admin roles can access this endpoint.
/// </summary>
public static class GetChallengeFlags
{
    public record Query(Guid Id) : IRequest<Result<IEnumerable<string>>>;

    private static readonly Error ChallengeNotFound = new("GetChallengeFlags.ChallengeNotFound",
        "The challenge with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<IEnumerable<string>>>
    {
        public async Task<Result<IEnumerable<string>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var challengeFlagsResponse = await cache.GetOrSetAsync(
                $"{nameof(Challenge)}.{nameof(Challenge.Flags)}:{request.Id}", async _ =>
                {
                    return await context
                        .Challenges
                        .Where(c => c.Id == request.Id)
                        .Select(c => c.Flags)
                        .FirstOrDefaultAsync(cancellationToken);
                }, token: cancellationToken);

            return challengeFlagsResponse ?? Result.Failure<IEnumerable<string>>(ChallengeNotFound);
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
                .RequireAuthorization(Constants.ManagerAdminOnly)
                .WithTags(nameof(Flags));
        }
    }
}