using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Submissions;

/// <summary>
/// Gets the leaderboards.
/// Shows full leaderboards if manager admin.
/// Shows top users if member.
/// </summary>
public static class GetLeaderboards
{
    public record Query(string RequesterId, bool IsMember) : IRequest<Result<LeaderboardsResponse>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<LeaderboardsResponse>>
    {
        public async Task<Result<LeaderboardsResponse>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var userRanks = await cache.GetOrSetAsync(Keys.UserRanks(), async _ =>
            {
                // Count all the user points and track the earliest submission time where the points are not zero
                var userPoints = await context.Submissions
                    .Where(s => s.IsCorrect)
                    .GroupBy(s => new { s.UserId, s.UserName })
                    .Select(g => new
                    {
                        g.Key.UserId,
                        g.Key.UserName,
                        TotalPoints = g.Sum(s => s.Challenge.Points),
                        EarliestNonZeroSubmission = g
                            .Where(s => s.Challenge.Points > 0)
                            .Min(s => s.SubmittedAt) // Track the earliest submission where points > 0
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

                // Combine points and deductions, calculate final score, sort by points, then by earliest non-zero submission time, and assign ranks
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
                            up.EarliestNonZeroSubmission
                        })
                    .OrderByDescending(u => u.FinalScore)
                    .ThenBy(u => u.EarliestNonZeroSubmission) // Break ties by earliest non-zero submission time
                    .Select((u, index) => new UserRankResponse
                    {
                        Id = u.UserId,
                        UserName = u.UserName,
                        Position = index + 1,
                        Points = u.FinalScore,
                        LatestCorrectSubmission = u.EarliestNonZeroSubmission
                    })
                    .ToList();

                return userRanks;
            }, new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(20) }, cancellationToken);

            var requesterRank = userRanks.FirstOrDefault(u => u.Id == request.RequesterId);

            var publicLeaderboardCount = await cache.GetOrSetAsync(Keys.PublicLeaderboardCount(),
                async _ => await context.GetPlayConfigurationValueAsync<int>(
                    Consts.PublicLeaderboardCount,
                    cancellationToken),
                token: cancellationToken);

            // Only show top users the requester is a member.
            if (request.IsMember)
                userRanks = userRanks.Take(publicLeaderboardCount).ToList();

            return new LeaderboardsResponse
            {
                RequesterRank = requesterRank,
                UserRanks = userRanks,
                RequesterIsMember = request.IsMember,
                PublicLeaderboardCount = publicLeaderboardCount
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
                    if (userId is null)
                        return Results.BadRequest();

                    var isMember = claims.GetRoles().Contains(Consts.Member);

                    var query = new Query(userId, isMember);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.StatusCode(500) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .WithTags(nameof(Submissions));
        }
    }
}