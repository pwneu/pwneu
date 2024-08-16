using System.ComponentModel.DataAnnotations;

namespace Pwneu.Play.Shared.Entities;

public class HintUsage
{
    [MaxLength(36)] public string UserId { get; init; } = string.Empty;
    public Guid HintId { get; init; }
    public DateTime UsedAt { get; init; }
    public Hint Hint { get; init; } = null!;
}