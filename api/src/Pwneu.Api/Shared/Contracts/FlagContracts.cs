namespace Pwneu.Api.Shared.Contracts;

public enum FlagStatus
{
    Incorrect,
    Correct,
    MaxAttemptReached,
    DeadlineReached,
    AlreadySolved,
    SubmittingTooOften
}