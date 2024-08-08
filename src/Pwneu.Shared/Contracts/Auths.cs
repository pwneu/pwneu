namespace Pwneu.Shared.Contracts;

public record RegisterRequest
{
    public string UserName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? AccessKey { get; set; }
}

public record LoginRequest
{
    public string UserName { get; set; } = default!;
    public string Password { get; set; } = default!;
}

public record LoggedInEvent
{
    public string FullName { get; set; } = default!;
    public string? Email { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Referer { get; set; }
}

public record RefreshRequest
{
    public string RefreshToken { get; set; } = default!;
    public string AccessToken { get; set; } = default!;
}

public record TokenResponse
{
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
}