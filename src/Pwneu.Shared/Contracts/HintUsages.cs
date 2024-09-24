namespace Pwneu.Shared.Contracts;

public record UserHintUsageResponse
{
    public Guid HintId { get; init; }
    public Guid ChallengeId { get; init; }
    public string ChallengeName { get; init; } = default!;
    public DateTime UsedAt { get; init; }
    public int Deduction { get; init; }
}

public record ChallengeHintUsageResponse
{
    public Guid HintId { get; init; }
    public string UserId { get; init; } = default!;
    public string UserName { get; init; } = default!;
    public DateTime UsedAt { get; init; }
    public int Deduction { get; init; }
}