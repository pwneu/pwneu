using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Challenges;

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
            var challenge = await cache.GetOrSetAsync(Keys.ChallengeDetails(request.Id), async _ =>
                await context
                    .Challenges
                    .GetDetailsByIdAsync(
                        request.Id,
                        cancellationToken), token: cancellationToken);

            // Cache the challenge flags because why not?
            await cache.GetOrSetAsync(Keys.Flags(request.Id), async _ =>
                await context
                    .Challenges
                    .Where(c => c.Id == request.Id)
                    .Select(c => c.Flags)
                    .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

            // Cache submissions allowed configuration because why not?
            await cache.GetOrSetAsync(Keys.SubmissionsAllowed(), async _ =>
                    await context.GetPlayConfigurationValueAsync<bool>(Consts.SubmissionsAllowed, cancellationToken),
                token: cancellationToken);

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