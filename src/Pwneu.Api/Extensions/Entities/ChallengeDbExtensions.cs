using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Extensions.Entities;

public static class ChallengeDbExtensions
{
    public static async Task<ChallengeDetailsResponse?> GetChallengeDetailsByIdAsync(
        this IFusionCache cache,
        AppDbContext context,
        Guid challengeId,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrSetAsync(
            CacheKeys.ChallengeDetails(challengeId),
            async _ => await context.GetChallengeDetailsByIdAsync(challengeId, cancellationToken),
            token: cancellationToken
        );
    }

    public static async Task<ChallengeDetailsResponse?> GetChallengeDetailsByIdAsync(
        this AppDbContext context,
        Guid challengeId,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Challenges.Where(ch => ch.Id == challengeId)
            .Include(ch => ch.Artifacts)
            .AsSplitQuery()
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
                Flags = ch.Flags,
                Artifacts = ch
                    .Artifacts.Select(a => new ArtifactResponse
                    {
                        Id = a.Id,
                        FileName = a.FileName,
                    })
                    .ToList(),
                Hints = ch
                    .Hints.Select(h => new HintResponse { Id = h.Id, Deduction = h.Deduction })
                    .ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
