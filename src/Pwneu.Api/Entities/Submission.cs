using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Entities;

public sealed class Submission
{
    public int Id { get; init; }

    [MaxLength(36)]
    public string UserId { get; init; } = string.Empty;
    public Guid ChallengeId { get; init; }
    public DateTime SubmittedAt { get; init; }
    public User User { get; init; } = null!;
    public Challenge Challenge { get; init; } = null!;

    private Submission() { }

    public static Submission Create(string userId, Guid challengeId)
    {
        return new Submission
        {
            UserId = userId,
            ChallengeId = challengeId,
            SubmittedAt = DateTime.UtcNow,
        };
    }

    public static Submission CreateFromBuffer(SubmissionBuffer buffer)
    {
        return new Submission
        {
            UserId = buffer.UserId,
            ChallengeId = buffer.ChallengeId,
            SubmittedAt = buffer.SubmittedAt,
        };
    }
}
