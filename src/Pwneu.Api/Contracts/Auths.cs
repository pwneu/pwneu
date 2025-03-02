using MediatR;
using System.Text.Json.Serialization;

namespace Pwneu.Api.Contracts;

public record RegisterRequest
{
    public string UserName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string AccessKey { get; set; } = default!;
    public string? TurnstileToken { get; set; }
}

public record RegisteredEvent : INotification
{
    public required string UserName { get; init; } = default!;
    public required string Email { get; init; } = default!;
    public required string FullName { get; init; } = default!;
    public required string ConfirmationToken { get; init; } = default!;
    public required string? IpAddress { get; set; }
}

public record LoginRequest
{
    public string UserName { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string? TurnstileToken { get; set; }
}

public record LoggedInEvent : INotification
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

public class TurnstileResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
}

public record VerifyEmailRequest
{
    public string Email { get; set; } = default!;
    public string ConfirmationToken { get; set; } = default!;
}
