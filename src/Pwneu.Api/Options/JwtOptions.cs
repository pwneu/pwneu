using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Options;

public sealed class JwtOptions
{
    [Required]
    public required string Issuer { get; init; }

    [Required]
    public required string Audience { get; init; }

    [Required]
    public required string SigningKey { get; init; }
}
