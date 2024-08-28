namespace Pwneu.Shared.Contracts;

public record ForgotPasswordEvent
{
    public required string Email { get; init; } = default!;
    public required string PasswordResetToken { get; init; } = default!;
}

public record PasswordResetRequest
{
    public string Email { get; init; } = default!;
    public string PasswordResetToken { get; init; } = default!;
    public string NewPassword { get; init; } = default!;
    public string RepeatPassword { get; init; } = default!;
}

public record PasswordResetEvent
{
    public string UserId { get; init; } = default!;
}