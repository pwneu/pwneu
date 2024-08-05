using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Shared.Entities;

public class AccessKey
{
    public Guid Id { get; init; }
    [MaxLength(100)] public string Key { get; init; } = string.Empty;
    public bool CanBeReused { get; init; }
    public DateTime Expiration { get; init; }
}