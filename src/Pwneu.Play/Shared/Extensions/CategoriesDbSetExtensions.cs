using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.Shared.Extensions;

public static class CategoriesDbSetExtensions
{
    public static async Task<UserCategoryEvalResponse?> GetUserEvaluationInCategoryAsync(
        this DbSet<Category> categories,
        string userId,
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return await categories
            .Where(c => c.Id == categoryId)
            .Select(c => new UserCategoryEvalResponse
            {
                CategoryId = c.Id,
                Name = c.Name,
                TotalChallenges = c.Challenges.Count,
                TotalSolves = c.Challenges
                    .SelectMany(ch => ch.Submissions)
                    .Count(s => s.UserId == userId && s.IsCorrect == true),
                IncorrectAttempts = c.Challenges
                    .SelectMany(ch => ch.Submissions)
                    .Count(s => s.UserId == userId && s.IsCorrect == false),
                HintsUsed = c.Challenges
                    .SelectMany(ch => ch.Hints)
                    .SelectMany(h => h.HintUsages)
                    .Count(hu => hu.UserId == userId)
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}