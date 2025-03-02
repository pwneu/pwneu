using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;

namespace Pwneu.Api.Features.Hints;

public static class GetChallengeHints
{
    public record Query(Guid ChallengeId) : IRequest<Result<IEnumerable<HintDetailsResponse>>>;

    private static readonly Error ChallengeNotFound = new(
        "GetHints.ChallengeNotFound",
        "The challenge with the specified ID was not found"
    );

    internal sealed class Handler(AppDbContext context)
        : IRequestHandler<Query, Result<IEnumerable<HintDetailsResponse>>>
    {
        public async Task<Result<IEnumerable<HintDetailsResponse>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var hints = await context
                .Challenges.Where(c => c.Id == request.ChallengeId)
                .Select(c =>
                    c.Hints.Select(h => new HintDetailsResponse
                    {
                        Id = h.Id,
                        Content = h.Content,
                        Deduction = h.Deduction,
                    })
                        .ToList()
                )
                .FirstOrDefaultAsync(cancellationToken);

            return hints ?? Result.Failure<IEnumerable<HintDetailsResponse>>(ChallengeNotFound);
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/challenges/{id:Guid}/hints",
                    async (Guid id, ISender sender) =>
                    {
                        var query = new Query(id);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.NotFound(result.Error)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .WithTags(nameof(Hints));
        }
    }
}
