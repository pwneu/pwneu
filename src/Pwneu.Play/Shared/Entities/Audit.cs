using System.ComponentModel.DataAnnotations;

namespace Pwneu.Play.Shared.Entities;

public class Audit
{
    public Guid Id { get; init; }
    [MaxLength(36)] public string UserId { get; init; } = string.Empty;
    [MaxLength(256)] public string UserName { get; init; } = string.Empty;
    [MaxLength(1000)] public string Action { get; init; } = string.Empty;
    public DateTime PerformedAt { get; init; }
}