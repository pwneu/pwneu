using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Entities;

public sealed class HintUsageBuffer
{
    public int Id { get; init; }

    [MaxLength(36)]
    public string UserId { get; init; } = string.Empty;
    public required Guid HintId { get; init; }
    public required int Deduction { get; init; }
    public required Guid ChallengeId { get; init; }
    public required string ChallengeName { get; init; }
    public required Guid CategoryId { get; init; }
    public required DateTime UsedAt { get; init; }

    private HintUsageBuffer() { }

    public static HintUsageBuffer Create(
        string userId,
        Guid hintId,
        int deduction,
        Guid challengeId,
        string challengeName,
        Guid categoryId,
        DateTime? usedAt = null
    )
    {
        return new HintUsageBuffer
        {
            UserId = userId,
            HintId = hintId,
            Deduction = deduction,
            ChallengeId = challengeId,
            ChallengeName = challengeName,
            UsedAt = usedAt ?? DateTime.UtcNow,
            CategoryId = categoryId,
        };
    }
}
