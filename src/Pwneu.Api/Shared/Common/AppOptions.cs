using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Shared.Common;

public sealed class AppOptions
{
    [Required] public required bool RequireRegistrationKey { get; init; } = false;
}