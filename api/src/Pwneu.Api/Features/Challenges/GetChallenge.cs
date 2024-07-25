using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Challenges;

public static class GetChallenge
{
    public record Query(Guid Id) : IRequest<Result<ChallengeDetailsResponse>>;

    private static readonly Error NotFound = new("GetChallenge.Null",
        "The challenge with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<ChallengeDetailsResponse>>
    {
        public async Task<Result<ChallengeDetailsResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var challengeResponse = await cache.GetOrSetAsync($"{nameof(ChallengeDetailsResponse)}:{request.Id}",
                async _ =>
                {
                    return await context
                        .Challenges
                        .Where(c => c.Id == request.Id)
                        .Include(c => c.ChallengeFiles)
                        .Select(c => new ChallengeDetailsResponse(c.Id, c.Name, c.Description, c.Points,
                            c.DeadlineEnabled, c.Deadline, c.MaxAttempts, c.ChallengeFiles
                                .Select(cf => new ChallengeFileResponse(cf.Id, cf.FileName))
                                .ToList()
                        ))
                        .FirstOrDefaultAsync(cancellationToken);
                }, token: cancellationToken);

            return challengeResponse ?? Result.Failure<ChallengeDetailsResponse>(NotFound);
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
                .WithTags(nameof(Challenge));
        }
    }
}