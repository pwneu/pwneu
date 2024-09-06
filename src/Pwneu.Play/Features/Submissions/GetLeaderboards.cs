using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Submissions;

public static class GetLeaderboards
{
    public record Query(string RequesterId) : IRequest<Result<LeaderboardsResponse>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<LeaderboardsResponse>>
    {
        public async Task<Result<LeaderboardsResponse>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var userRanks = await cache.GetOrSetAsync(Keys.UserRanks(), async _ =>
            {
                // Count all the user points and track the earliest submission time for tie-breaking
                var userPoints = await context.Submissions
                    .Where(s => s.IsCorrect)
                    .GroupBy(s => new { s.UserId, s.UserName })
                    .Select(g => new
                    {
                        g.Key.UserId,
                        g.Key.UserName,
                        TotalPoints = g.Sum(s => s.Challenge.Points),
                        EarliestSubmission = g.Min(s => s.SubmittedAt) // Track the earliest submission time
                    })
                    .ToListAsync(cancellationToken);

                // Count all the user deductions of hint usages
                var userDeductions = await context.HintUsages
                    .GroupBy(hu => new { hu.UserId })
                    .Select(g => new
                    {
                        g.Key.UserId,
                        TotalDeductions = g.Sum(hu => hu.Hint.Deduction)
                    })
                    .ToListAsync(cancellationToken);

                // Combine points and deductions, calculate final score, sort by points, then by earliest submission time, and assign ranks
                var userRanks = userPoints
                    .GroupJoin(
                        userDeductions,
                        up => up.UserId,
                        ud => ud.UserId,
                        (up, uds) => new
                        {
                            up.UserId,
                            up.UserName,
                            FinalScore = up.TotalPoints - uds.Sum(ud => ud.TotalDeductions),
                            up.EarliestSubmission
                        })
                    .OrderByDescending(u => u.FinalScore)
                    .ThenBy(u => u.EarliestSubmission) // Break ties by earliest submission time
                    .Select((u, index) => new UserRankResponse
                    {
                        Id = u.UserId,
                        UserName = u.UserName,
                        Position = index + 1,
                        Points = u.FinalScore
                    })
                    .ToList();

                return userRanks;
            }, token: cancellationToken);

            var requesterRank = userRanks.FirstOrDefault(u => u.Id == request.RequesterId);

            return new LeaderboardsResponse
            {
                RequesterRank = requesterRank,
                UserRanks = userRanks
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
                .WithTags(nameof(Submissions));
        }
    }
}