using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.Shared.Extensions;

public static class ApplicationDbContextExtensions
{
    public static async Task<List<UserRankResponse>> GetUserRanks(
        this ApplicationDbContext context,
        CancellationToken cancellationToken = default)
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
    }
}