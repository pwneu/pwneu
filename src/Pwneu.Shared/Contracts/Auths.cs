namespace Pwneu.Shared.Contracts;

public record RegisterRequest
{
    public string UserName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? AccessKey { get; set; }
}

public record RegisteredEvent
{
    public string Email { get; init; } = default!;
    public string ConfirmationToken { get; init; } = default!;
}

public record ConfirmEmailRequest
{
    public string Email { get; set; } = default!;
    public string ConfirmationToken { get; set; } = default!;
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

public record LoginResponse
{
    public string Id { get; set; } = default!;
    public string? UserName { get; set; } = default!;
    public List<string> Roles { get; set; } = [];
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
}

public record TokenResponse
{
    public string Id { get; set; } = default!;
    public string? UserName { get; set; } = default!;
    public List<string> Roles { get; set; } = [];
    public string AccessToken { get; set; } = default!;
}