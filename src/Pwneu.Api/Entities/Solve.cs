using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Entities;

public sealed class Solve
{
    public int Id { get; init; }

    [MaxLength(36)]
    public required string UserId { get; init; } = string.Empty;
    public required Guid ChallengeId { get; init; }
    public required DateTime SolvedAt { get; init; }
    public User User { get; init; } = null!;
    public Challenge Challenge { get; init; } = null!;

    public Solve() { }

    public static Solve Create(string userId, Guid challengeId)
    {
        return new Solve
        {
            UserId = userId,
            ChallengeId = challengeId,
            SolvedAt = DateTime.UtcNow,
        };
    }

    public static Solve CreateFromBuffer(SolveBuffer buffer)
    {
        return new Solve
        {
            UserId = buffer.UserId,
            ChallengeId = buffer.ChallengeId,
            SolvedAt = buffer.SolvedAt,
        };
    }
}
