namespace Pwneu.Api.Shared.Contracts;

public enum SubmitFlagResponse
{
    Incorrect,
    Correct,
    MaxAttemptReached,
    DeadlineReached,
    AlreadySolved,
}