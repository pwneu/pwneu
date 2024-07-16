using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;

namespace Pwneu.Api.Features.Challenges;

public static class GetChallenge
{
    public record Query(Guid Id) : IRequest<Result<ChallengeResponse>>;

    internal sealed class Handler(ApplicationDbContext context) : IRequestHandler<Query, Result<ChallengeResponse>>
    {
        public async Task<Result<ChallengeResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var challengeResponse = await context
                .Challenges
                .Where(c => c.Id == request.Id)
                .Select(c =>
                    new ChallengeResponse(c.Id, c.Name, c.Description, c.Points, c.DeadlineEnabled, c.Deadline,
                        c.MaxAttempts))
                .FirstOrDefaultAsync(cancellationToken);

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