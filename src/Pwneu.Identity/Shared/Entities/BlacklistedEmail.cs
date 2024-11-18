using System.ComponentModel.DataAnnotations;

namespace Pwneu.Identity.Shared.Entities;

public class BlacklistedEmail
{
    public Guid Id { get; init; }
    [MaxLength(100)] public string Email { get; init; } = string.Empty;
}