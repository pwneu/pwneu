namespace Pwneu.Api.Shared.Contracts;

public record ChallengeResponse(Guid Id, string Name);

public record ChallengeDetailsResponse(
    Guid Id,
    string Name,
    string Description,
    int Points,
    bool DeadlineEnabled,
    DateTime Deadline,
    int MaxAttempts,
    int SolveCount,
    IEnumerable<ChallengeFileResponse> ChallengeFiles);

public record CreateChallengeRequest(
    string Name,
    string Description,
    int Points,
    bool DeadlineEnabled,
    DateTime Deadline,
    int MaxAttempts,
    IEnumerable<string> Flags);

public record UpdateChallengeRequest(
    string Name,
    string Description,
    int Points,
    bool DeadlineEnabled,
    DateTime Deadline,
    int MaxAttempts,
    IEnumerable<string> Flags);