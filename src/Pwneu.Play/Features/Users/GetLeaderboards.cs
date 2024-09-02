using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Users;

// TODO -- Test this

public static class GetLeaderboards
{
    public record Query(string RequesterId) : IRequest<Result<LeaderboardsResponse>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<LeaderboardsResponse>>
    {
        public async Task<Result<LeaderboardsResponse>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var leaderboards = await cache.GetOrSetAsync(Keys.UserRanks(), async _ =>
            {
                // Step 1: Calculate total points from correct submissions, considering challenge points
                var userPoints = await context.Submissions
                    .Where(s => s.IsCorrect)
                    .Join(context.Challenges,
                        s => s.ChallengeId,
                        c => c.Id,
                        (s, c) => new
                        {
                            s.UserId,
                            c.Points
                        })
                    .GroupBy(sc => sc.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        Points = g.Sum(sc => sc.Points)
                    })
                    .ToListAsync(cancellationToken);

                // Step 2: Calculate total deductions from hint usages
                var userDeductions = await context.HintUsages
                    .Join(context.Hints,
                        hu => hu.HintId,
                        h => h.Id,
                        (hu, h) => new
                        {
                            hu.UserId, h.Deduction
                        })
                    .GroupBy(hu => hu.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        Deductions = g.Sum(hu => hu.Deduction)
                    })
                    .ToListAsync(cancellationToken);

                // Step 3: Track the timestamp of the first correct submission for each user
                var firstSubmissionTimes = await context.Submissions
                    .Where(s => s.IsCorrect)
                    .GroupBy(s => s.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        FirstSubmissionTime = g.Min(s => s.SubmittedAt)
                    })
                    .ToListAsync(cancellationToken);

                // Step 4: Combine points, deductions, and first submission times
                var userPointsAndDeductions = userPoints
                    .Join(userDeductions,
                        p => p.UserId,
                        d => d.UserId,
                        (p, d) => new
                        {
                            p.UserId,
                            NetPoints = p.Points - d.Deductions
                        })
                    .ToList();

                var userRanks = firstSubmissionTimes
                    .Join(userPointsAndDeductions,
                        t => t.UserId,
                        pd => pd.UserId,
                        (t, pd) => new
                        {
                            t.UserId,
                            pd.NetPoints,
                            t.FirstSubmissionTime
                        })
                    .OrderByDescending(r => r.NetPoints)
                    .ThenBy(r => r.FirstSubmissionTime) // Sort by the first submission time for tie-breaking
                    .Select(r => new UserRankResponse
                    {
                        Id = r.UserId,
                        Points = r.NetPoints,
                        Position = 0 // Placeholder; you will set positions in the final step
                    })
                    .ToList();

                // Step 5: Assign positions
                for (var i = 0; i < userRanks.Count; i++)
                {
                    userRanks[i] = userRanks[i] with { Position = i + 1 };
                }

                return userRanks;
            }, token: cancellationToken);

            var requesterRank = leaderboards.FirstOrDefault(u => u.Id == request.RequesterId);

            return new LeaderboardsResponse
            {
                RequesterRank = requesterRank,
                TopUsers = leaderboards
            };
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("leaderboards", async (ClaimsPrincipal claims, ISender sender) =>
                {
                    var userId = claims.GetLoggedInUserId<string>();
                    if (userId is null) return Results.BadRequest();

                    var query = new Query(userId);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.StatusCode(500) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .WithTags(nameof(Users));
        }
    }
}