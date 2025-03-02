namespace Pwneu.Api.Contracts;

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

public enum ChallengeStatus
{
    Disabled,
    AlreadySolved,
    Allowed
}

public record RecalculateRequest;
