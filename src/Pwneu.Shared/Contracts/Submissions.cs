namespace Pwneu.Shared.Contracts;

public record UserSolveResponse
{
    public Guid ChallengeId { get; set; }
    public string ChallengeName { get; set; } = default!;
    public int Points { get; set; }
    public DateTime SolvedAt { get; set; }
}

public record ChallengeSolveResponse
{
    public string UserId { get; set; } = default!;
    public string? UserName { get; set; }
    public DateTime SolvedAt { get; set; }
}

public enum ChallengeStatus
{
    Disabled,
    AlreadySolved,
    Allowed
}

public enum FlagStatus
{
    Incorrect,
    Correct,
    MaxAttemptReached,
    DeadlineReached,
    AlreadySolved,
    SubmittingTooOften,
    SubmissionsNotAllowed
}