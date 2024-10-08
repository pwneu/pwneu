using System.ComponentModel.DataAnnotations;

namespace Pwneu.Identity.Shared.Entities;

public class IdentityConfiguration
{
    [MaxLength(100)] public string Key { get; init; } = null!;
    [MaxLength(100)] public string Value { get; set; } = null!;
}