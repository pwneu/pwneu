using System.ComponentModel.DataAnnotations;

namespace Pwneu.Play.Shared.Entities;

public class Hint
{
    public Guid Id { get; init; }
    public Guid ChallengeId { get; init; }
    [MaxLength(100)] public string Content { get; init; } = string.Empty;
    public int Deduction { get; init; }
    public Challenge Challenge { get; init; } = null!;
    public ICollection<HintUsage> HintUsages { get; init; } = [];
}