namespace Pwneu.Shared.Contracts;

public enum FlagStatus
{
    Incorrect,
    Correct,
    MaxAttemptReached,
    DeadlineReached,
    AlreadySolved,
    SubmittingTooOften
}

public record SubmittedEvent
{
    public string UserId { get; set; } = default!;
    public Guid ChallengeId { get; set; }
    public string Flag { get; set; } = default!;
    public DateTime SubmittedAt { get; set; }
    public bool IsCorrect { get; set; }
}