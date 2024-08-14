using System.ComponentModel.DataAnnotations;

namespace Pwneu.Identity.Shared.Options;

public sealed class AppOptions
{
    [Required] public required bool RequireRegistrationKey { get; init; }
    [Required] public required string InitialAdminPassword { get; init; }
}