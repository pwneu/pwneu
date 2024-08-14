using System.ComponentModel.DataAnnotations;

namespace Pwneu.Identity.Shared.Options;

public sealed class JwtOptions
{
    [Required] public required string Issuer { get; init; }

    [Required] public required string Audience { get; init; }

    [Required] public required string SigningKey { get; init; }
}