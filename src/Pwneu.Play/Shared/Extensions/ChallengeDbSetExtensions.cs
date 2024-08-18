using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.Shared.Extensions;

public static class ChallengeDbSetExtensions
{
    public static async Task<ChallengeDetailsResponse?> GetDetailsByIdAsync(
        this DbSet<Challenge> challenges,
        Guid challengeId,
        CancellationToken cancellationToken = default)
    {
        return await challenges
            .Where(c => c.Id == challengeId)
            .Include(c => c.Artifacts)
            .Select(c => new ChallengeDetailsResponse
            {
                Id = c.Id,
                CategoryId = c.CategoryId,
                CategoryName = c.Category.Name,
                Name = c.Name,
                Description = c.Description,
                Points = c.Points,
                DeadlineEnabled = c.DeadlineEnabled,
                Deadline = c.Deadline,
                MaxAttempts = c.MaxAttempts,
                SolveCount = c.Submissions.Count(s => s.IsCorrect == true),
                Artifacts = c.Artifacts
                    .Select(a => new ArtifactResponse
                    {
                        Id = a.Id,
                        FileName = a.FileName,
                    }).ToList(),
                Hints = c.Hints
                    .Select(h => new HintResponse
                    {
                        Id = h.Id,
                        Deduction = h.Deduction
                    }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}