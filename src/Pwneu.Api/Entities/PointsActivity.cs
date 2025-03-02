using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Entities;

public sealed class PointsActivity
{
    public int Id { get; init; }

    [MaxLength(36)]
    public required string UserId { get; init; }
    public required bool IsSolve { get; init; }
    public required Guid ChallengeId { get; init; }
    public required Guid HintId { get; init; }

    [MaxLength(100)]
    public required string ChallengeName { get; init; }
    public required int PointsChange { get; init; }
    public required DateTime OccurredAt { get; init; }
    public User User { get; init; } = null!;

    private PointsActivity() { }

    public static PointsActivity CreateFromSolveBuffer(SolveBuffer solveBuffer)
    {
        return new PointsActivity
        {
            UserId = solveBuffer.UserId,
            IsSolve = true,
            ChallengeId = solveBuffer.ChallengeId,
            HintId = Guid.Empty,
            ChallengeName = solveBuffer.ChallengeName,
            PointsChange = solveBuffer.Points,
            OccurredAt = solveBuffer.SolvedAt,
        };
    }

    public static PointsActivity CreateFromHintUsageBuffer(HintUsageBuffer hintUsageBuffer)
    {
        return new PointsActivity
        {
            UserId = hintUsageBuffer.UserId,
            IsSolve = false,
            ChallengeId = hintUsageBuffer.ChallengeId,
            HintId = hintUsageBuffer.HintId,
            ChallengeName = hintUsageBuffer.ChallengeName,
            PointsChange = -hintUsageBuffer.Deduction,
            OccurredAt = hintUsageBuffer.UsedAt,
        };
    }
}
