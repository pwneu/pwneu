using System.ComponentModel.DataAnnotations;

namespace Pwneu.Identity.Shared.Options;

public sealed class AppOptions
{
    [Required] public required string InitialAdminPassword { get; init; }
    public required string? ValidEmailDomain { get; init; }
    [Required] public required bool IsTurnstileEnabled { get; init; }
    [Required] public required string TurnstileSecretKey { get; init; }
    [Required] public required string ResetPasswordUrl { get; init; }
}