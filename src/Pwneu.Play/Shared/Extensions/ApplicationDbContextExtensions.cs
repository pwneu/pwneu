using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.Shared.Extensions;

public static class ApplicationDbContextExtensions
{
    /// <summary>
    /// Gets the ranks of all members.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>List of user rankings</returns>
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

    /// <summary>
    /// Gets the graph of users by user ids.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="userIds"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<List<List<UserActivityResponse>>> GetUsersGraph(
        this ApplicationDbContext context,
        string[] userIds,
        CancellationToken cancellationToken = default)
    {
        // Get the list of correct submissions of the users.
        var correctSubmissions = await context
            .Submissions
            .Where(s => userIds.Contains(s.UserId) && s.IsCorrect)
            .Select(s => new UserActivityResponse
            {
                UserId = s.UserId,
                UserName = s.UserName,
                ActivityDate = s.SubmittedAt,
                Score = s.Challenge.Points
            })
            .ToListAsync(cancellationToken);

        // Get the list of hint usages of the users but store the score in negative form.
        var hintUsages = await context
            .HintUsages
            .Where(h => userIds.Contains(h.UserId))
            .Select(h => new UserActivityResponse
            {
                UserId = h.UserId,
                UserName = h.UserName,
                ActivityDate = h.UsedAt,
                Score = -h.Hint.Deduction
            })
            .ToListAsync(cancellationToken);

        // Combine both lists.
        var allActivities = correctSubmissions.Concat(hintUsages);

        // Group by UserId and calculate cumulative scores.
        var usersGraph = allActivities
            .GroupBy(a => a.UserId)
            .Select(g =>
            {
                var activities =
                    g.OrderBy(a => a.ActivityDate)
                        .ToList(); // Order activities by date for cumulative score calculation.

                // Initialize cumulative score and a list to keep track of the earliest date each score is reached.
                var cumulativeScore = 0;
                var scoreReachDates = new List<(int CumulativeScore, DateTime Date)>();

                // Update the score in each activity to reflect the cumulative score.
                foreach (var activity in activities)
                {
                    cumulativeScore += activity.Score;
                    activity.Score = cumulativeScore; // Set the cumulative score in the activity.

                    // Track when each cumulative score was reached.
                    scoreReachDates.Add((cumulativeScore, activity.ActivityDate));
                }

                // Determine the earliest date when the final cumulative score was achieved.
                var earliestDateForFinalScore = scoreReachDates
                    .Where(s => s.CumulativeScore == cumulativeScore)
                    .Min(s => s.Date);

                // Return the activities along with the final cumulative score and the earliest date of that score.
                return new
                {
                    UserId = g.Key,
                    Activities = activities,
                    FinalCumulativeScore = cumulativeScore,
                    EarliestDateForFinalScore = earliestDateForFinalScore
                };
            })
            .OrderByDescending(x => x.FinalCumulativeScore) // Sort by final cumulative score descending.
            .ThenBy(x => x.EarliestDateForFinalScore) // If tied, sort by the earliest date the score was reached.
            .Select(x => x.Activities) // Select only the activities for the final result.
            .ToList();

        return usersGraph;
    }
}