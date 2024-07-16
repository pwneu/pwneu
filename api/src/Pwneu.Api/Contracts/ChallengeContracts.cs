namespace Pwneu.Api.Contracts;

public record ChallengeResponse(
    Guid Id,
    string Name,
    string Description,
    int Points,
    bool DeadlineEnabled,
    DateTime Deadline,
    int MaxAttempts);

public record CreateChallengeRequest(
    string Name,
    string Description,
    int Points,
    bool DeadlineEnabled,
    DateTime Deadline,
    int MaxAttempts);

public record UpdateChallengeRequest(
    Guid Id,
    string Name,
    string Description,
    int Points,
    bool DeadlineEnabled,
    DateTime Deadline,
    int MaxAttempts);