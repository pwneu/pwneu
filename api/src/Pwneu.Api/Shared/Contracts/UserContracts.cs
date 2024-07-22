namespace Pwneu.Api.Shared.Contracts;

public record UserResponse(string Id, string? UserName);

public record CreateUserRequest(string UserName, string Email, string Password, string FullName);

public record LoginRequest(string UserName, string Password);