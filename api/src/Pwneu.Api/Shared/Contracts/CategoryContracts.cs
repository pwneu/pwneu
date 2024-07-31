namespace Pwneu.Api.Shared.Contracts;

public record CategoryResponse(
    Guid Id,
    string Name,
    string Description,
    IEnumerable<ChallengeResponse> Challenges);

public record CategoryEvalResponse(
    Guid CategoryId,
    string CategoryName,
    int TotalChallenges,
    int TotalSolves,
    int CorrectAttempts,
    int IncorrectAttempts);

public record CreateCategoryRequest(string Name, string Description);

public record UpdateCategoryRequest(string Name, string Description);