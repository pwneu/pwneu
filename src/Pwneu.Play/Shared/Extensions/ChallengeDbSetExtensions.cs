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
            .Where(ch => ch.Id == challengeId)
            .Include(ch => ch.Artifacts)
            .Select(ch => new ChallengeDetailsResponse
            {
                Id = ch.Id,
                CategoryId = ch.CategoryId,
                CategoryName = ch.Category.Name,
                Name = ch.Name,
                Description = ch.Description,
                Points = ch.Points,
                DeadlineEnabled = ch.DeadlineEnabled,
                Deadline = ch.Deadline,
                MaxAttempts = ch.MaxAttempts,
                SolveCount = ch.SolveCount,
                Tags = ch.Tags,
                Artifacts = ch.Artifacts
                    .Select(a => new ArtifactResponse
                    {
                        Id = a.Id,
                        FileName = a.FileName,
                    }).ToList(),
                Hints = ch.Hints
                    .Select(h => new HintResponse
                    {
                        Id = h.Id,
                        Deduction = h.Deduction
                    }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}