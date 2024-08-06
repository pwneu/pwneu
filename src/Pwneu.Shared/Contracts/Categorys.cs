namespace Pwneu.Shared.Contracts;

public record CategoryResponse(
    Guid Id,
    string Name,
    string Description,
    IEnumerable<ChallengeResponse> Challenges);

public record CategoryEvalResponse(
    Guid Id,
    string Name,
    int TotalChallenges,
    int TotalSolves,
    int IncorrectAttempts);

public record CreateCategoryRequest(string Name, string Description);

public record UpdateCategoryRequest(string Name, string Description);