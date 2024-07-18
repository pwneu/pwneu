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
    public record Query(Guid Id) : IRequest<Result<ChallengeResponse>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<ChallengeResponse>>
    {
        public async Task<Result<ChallengeResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var challengeResponse = await cache.GetOrSetAsync($"{nameof(ChallengeResponse)}:{request.Id}", async _ =>
            {
                var challengeResponse = await context
                    .Challenges
                    .Where(c => c.Id == request.Id)
                    .Include(c => c.ChallengeFiles)
                    .Select(c => new ChallengeResponse(c.Id, c.Name, c.Description, c.Points, c.DeadlineEnabled,
                        c.Deadline,
                        c.MaxAttempts, c.ChallengeFiles
                            .Select(cf => new ChallengeFileResponse(cf.Id, cf.FileName))
                            .ToList()
                    ))
                    .FirstOrDefaultAsync(cancellationToken);

                return challengeResponse;
            }, token: cancellationToken);

            if (challengeResponse is null)
                return Result.Failure<ChallengeResponse>(new Error("GetChallenge.Null",
                    "The challenge with the specified ID was not found"));

            return challengeResponse;
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