namespace Pwneu.Api.Shared.Contracts;

public record RegisterRequest(string UserName, string Email, string Password, string FullName);

public record LoginRequest(string UserName, string Password);