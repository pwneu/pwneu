using System.ComponentModel.DataAnnotations;

namespace Pwneu.Identity.Shared.Options;

public sealed class AppOptions
{
    [Required] public required bool RequireEmailVerification { get; init; }
    [Required] public required string InitialAdminPassword { get; init; }
    public required string? ValidEmailDomain { get; init; }
}