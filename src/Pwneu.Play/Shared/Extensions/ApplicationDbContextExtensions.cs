using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.Shared.Extensions;

public static class ApplicationDbContextExtensions
{
    /// <summary>
    /// Gets the ranks of all members, tracking who reached the score first.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>List of user rankings</returns>
    public static async Task<List<UserRankResponse>> GetUserRanksAsync(
        this ApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        // Count all the user points and track the latest solve time where the points are not zero
        var userPoints = await context.Solves
            .GroupBy(s => new { s.UserId, s.UserName })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.UserName,
                TotalPoints = g.Sum(s => s.Challenge.Points),
                LatestNonZeroSolve = g
                    .Where(s => s.Challenge.Points > 0)
                    .Max(s => s.SolvedAt) // Track the latest solve where points > 0
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

        // Combine points and deductions, calculate final score, sort by points, then by latest non-zero solve time, and assign ranks
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
                    up.LatestNonZeroSolve
                })
            .OrderByDescending(u => u.FinalScore)
            .ThenBy(u => u.LatestNonZeroSolve) // Break ties by the earliest time the final score was reached
            .Select((u, index) => new UserRankResponse
            {
                Id = u.UserId,
                UserName = u.UserName,
                Position = index + 1,
                Points = u.FinalScore,
                LatestSolve = u.LatestNonZeroSolve
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
    public static async Task<UsersGraphResponse> GetUsersGraphAsync(
        this ApplicationDbContext context,
        string[] userIds,
        CancellationToken cancellationToken = default)
    {
        // Get the list of correct submissions of the users.
        var correctSubmissions = await context
            .Solves
            .Where(s => userIds.Contains(s.UserId))
            .Select(s => new UserActivityResponse
            {
                UserId = s.UserId,
                UserName = s.UserName,
                ActivityDate = s.SolvedAt,
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

        // Combine both lists into allActivities.
        var allActivities = correctSubmissions.Concat(hintUsages).ToList();

        // If no activities exist, return empty response.
        if (allActivities.Count == 0)
        {
            return new UsersGraphResponse
            {
                UsersGraph = [],
                GraphLabels = []
            };
        }

        // Find the earliest and latest submission dates from all activities.
        var earliestSubmission = allActivities.Min(a => a.ActivityDate);
        var latestSubmission = allActivities.Max(a => a.ActivityDate);

        // Group activities by UserId and calculate cumulative scores.
        var usersGraph = allActivities
            .GroupBy(a => a.UserId)
            .Select(g =>
            {
                var activities =
                    g.OrderBy(a => a.ActivityDate)
                        .ToList(); // Order activities by date for cumulative score calculation.

                // Initialize cumulative score.
                var cumulativeScore = 0;

                // Update the score in each activity to reflect the cumulative score.
                foreach (var activity in activities)
                {
                    cumulativeScore += activity.Score;
                    activity.Score = cumulativeScore; // Set the cumulative score in the activity.
                }

                return activities;
            })
            .OrderByDescending(a => a.Last().Score) // Sort by final cumulative score descending.
            .ThenBy(a => a.First().ActivityDate) // If tied, sort by the earliest date the score was reached.
            .ToList();

        // Calculate the maximum number of activities for the graph labels.
        var maxActivitiesCount = usersGraph.Max(g => g.Count);

        // If there's only one activity for a user, create a single label.
        // This avoids any potential errors when trying to calculate intervals 
        // for graph labels since there's no range between earliest and latest 
        // submission dates. Thus, we simply use the earliest date as the label.
        if (maxActivitiesCount == 1)
        {
            return new UsersGraphResponse
            {
                UsersGraph = usersGraph,
                GraphLabels = [earliestSubmission]
            };
        }

        // Calculate equally spaced labels between the earliest and latest submission dates.
        // The interval is determined by dividing the total time span by the 
        // number of intervals needed (maxActivitiesCount - 1).
        var interval = (latestSubmission - earliestSubmission).TotalMilliseconds / (maxActivitiesCount - 1);
        var graphLabels = new List<DateTime>();

        // Create labels for the graph based on the calculated interval.
        for (var i = 0; i < maxActivitiesCount; i++)
        {
            var label = earliestSubmission.AddMilliseconds(interval * i);
            graphLabels.Add(label);
        }

        return new UsersGraphResponse
        {
            UsersGraph = usersGraph,
            GraphLabels = graphLabels
        };
    }

    /// <summary>
    /// Returns the activity graph of a user.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="userId">The id of user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    public static async Task<List<UserActivityResponse>> GetUserGraphAsync(
        this ApplicationDbContext context,
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Get the list of correct submissions by the user.
        var correctSubmissions = await context
            .Solves
            .Where(s => s.UserId == userId)
            .Select(s => new UserActivityResponse
            {
                UserId = s.UserId,
                UserName = s.UserName,
                ActivityDate = s.SolvedAt,
                Score = s.Challenge.Points
            })
            .ToListAsync(cancellationToken);

        // Get the list of hint usages by the user but store the score in negative form.
        var hintUsages = await context
            .HintUsages
            .Where(h => h.UserId == userId)
            .Select(h => new UserActivityResponse
            {
                UserId = h.UserId,
                UserName = h.UserName,
                ActivityDate = h.UsedAt,
                Score = -h.Hint.Deduction
            })
            .ToListAsync(cancellationToken);

        // Combine correct submissions and hint usages.
        var userGraph = correctSubmissions
            .Concat(hintUsages)
            .OrderBy(a => a.ActivityDate)
            .ToList();

        // Store the cumulative score and update the score of each item in the list.
        var cumulativeScore = 0;
        foreach (var userActivity in userGraph)
        {
            cumulativeScore += userActivity.Score;
            userActivity.Score = cumulativeScore;
        }

        return userGraph;
    }
}