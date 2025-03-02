using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Entities;

public sealed class SolveBuffer
{
    public int Id { get; init; }

    [MaxLength(36)]
    public required string UserId { get; init; } = string.Empty;
    public required Guid ChallengeId { get; init; }
    public required string ChallengeName { get; init; }
    public required int Points { get; init; }
    public required Guid CategoryId { get; init; }
    public required DateTime SolvedAt { get; init; }

    public SolveBuffer() { }

    public static SolveBuffer Create(
        string userId,
        Guid challengeId,
        string challengeName,
        int points,
        Guid categoryId,
        DateTime? solvedAt = null
    )
    {
        return new SolveBuffer
        {
            UserId = userId,
            ChallengeId = challengeId,
            ChallengeName = challengeName,
            Points = points,
            CategoryId = categoryId,
            SolvedAt = solvedAt ?? DateTime.UtcNow,
        };
    }
}
