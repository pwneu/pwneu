using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Challenges;

/// <summary>
/// Retrieves a challenge by ID.
/// </summary>
public static class GetChallenge
{
    public record Query(Guid Id) : IRequest<Result<ChallengeDetailsResponse>>;

    private static readonly Error NotFound = new("GetChallenge.Null",
        "The challenge with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<ChallengeDetailsResponse>>
    {
        public async Task<Result<ChallengeDetailsResponse>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var challenge = await cache.GetOrSetAsync(Keys.Challenge(request.Id), async _ =>
                await context
                    .Challenges
                    .Where(c => c.Id == request.Id)
                    .Include(c => c.Artifacts)
                    .Select(c => new ChallengeDetailsResponse(c.Id, c.Name, c.Description, c.Points,
                        c.DeadlineEnabled, c.Deadline, c.MaxAttempts, c.Solves.Count, c.Artifacts
                            .Select(a => new ArtifactResponse(a.Id, a.FileName))
                            .ToList()
                    ))
                    .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

            return challenge ?? Result.Failure<ChallengeDetailsResponse>(NotFound);
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("challenges/{id:Guid}", async (Guid id, ISender sender) =>
                {
                    var query = new Query(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .WithTags(nameof(Challenges));
        }
    }
}