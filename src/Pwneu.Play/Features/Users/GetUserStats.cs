using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.Features.Users;

public static class GetUserStats
{
    public record Query(string Id) : IRequest<Result<UserStatsResponse>>;

    private static readonly Error NotFound = new("GetUserStats.NotFound",
        "The user with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IMemberAccess memberAccess)
        : IRequestHandler<Query, Result<UserStatsResponse>>
    {
        public async Task<Result<UserStatsResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            // Check if user exists.
            if (!await memberAccess.MemberExistsAsync(request.Id, cancellationToken))
                return Result.Failure<UserStatsResponse>(NotFound);

            // TODO -- Cache member stats
            var userStats = //await cache.GetOrSetAsync(Keys.UserStats(request.Id), async _ =>
                new UserStatsResponse
                {
                    Id = request.Id,
                    Evaluations = await context
                        .Categories
                        .Select(c => new CategoryEvalResponse
                        {
                            Id = c.Id,
                            Name = c.Name,
                            TotalChallenges = c.Challenges.Count,
                            TotalSolves = c.Challenges
                                .SelectMany(ch => ch.Submissions)
                                .Count(s => s.UserId == request.Id && s.IsCorrect == true),
                            IncorrectAttempts = c.Challenges
                                .SelectMany(ch => ch.Submissions)
                                .Count(s => s.UserId == request.Id && s.IsCorrect == false)
                        })
                        .ToListAsync(cancellationToken)
                };
            // , token: cancellationToken);

            return userStats;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("members/{id:Guid}/stats/", async (Guid id, ISender sender) =>
                {
                    var query = new Query(id.ToString());
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Users));
        }
    }
}