using System.ComponentModel.DataAnnotations;

namespace Pwneu.Identity.Shared.Entities;

public class Certificate
{
    public Guid Id { get; init; }
    [MaxLength(36)] public string UserId { get; init; } = string.Empty;
    [MaxLength(100)] public string FullName { get; init; } = string.Empty;
    [MaxLength(100)] public string Issuer { get; init; } = string.Empty;
    public DateTime IssuedAt { get; init; }
}