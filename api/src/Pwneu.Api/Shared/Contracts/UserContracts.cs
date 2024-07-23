namespace Pwneu.Api.Shared.Contracts;

public record UserResponse(string Id, string? UserName);

public record UserDetailsResponse(
    string Id,
    string? UserName,
    string? Email,
    string FullName,
    DateTime CreatedAt,
    IEnumerable<SolveResponse> SolvedChallenges);

public record CreateUserRequest(string UserName, string Email, string Password, string FullName);

public record LoginRequest(string UserName, string Password);