namespace Pwneu.Api.Shared.Contracts;

public record UserResponse(string Id, string? UserName);

public record UserDetailsResponse(
    string Id,
    string? UserName,
    string? Email,
    string FullName,
    DateTime CreatedAt,
    int TotalPoints,
    int CorrectAttempts,
    int IncorrectAttempts);

public record UserStatsResponse(string Id, IEnumerable<CategoryEvalResponse> Evaluations);
