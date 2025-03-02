using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Entities;

public sealed class SubmissionBuffer
{
    public int Id { get; init; }

    [MaxLength(36)]
    public string UserId { get; init; } = string.Empty;
    public Guid ChallengeId { get; init; }
    public DateTime SubmittedAt { get; init; }

    private SubmissionBuffer() { }

    public static SubmissionBuffer Create(
        string userId,
        Guid challengeId,
        DateTime? submittedAt = null
    )
    {
        return new SubmissionBuffer
        {
            UserId = userId,
            ChallengeId = challengeId,
            SubmittedAt = submittedAt ?? DateTime.UtcNow,
        };
    }
}
