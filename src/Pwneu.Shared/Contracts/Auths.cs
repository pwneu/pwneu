namespace Pwneu.Shared.Contracts;

public record RegisterRequest(string UserName, string Email, string Password, string FullName, string? AccessKey);

public record LoginRequest(string UserName, string Password);

public record LoggedInEvent(
    string FullName,
    string? Email,
    string? IpAddress = null,
    string? UserAgent = null,
    string? Referer = null);

public record RefreshRequest(string RefreshToken, string AccessToken);

public record TokenResponse(string AccessToken, string RefreshToken);