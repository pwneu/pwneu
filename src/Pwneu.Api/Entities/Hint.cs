using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Entities;

public sealed class Hint
{
    public Guid Id { get; init; }
    public Guid ChallengeId { get; init; }

    [MaxLength(500)]
    public string Content { get; init; } = string.Empty;
    public int Deduction { get; init; }
    public Challenge Challenge { get; init; } = null!;
    public ICollection<HintUsage> HintUsages { get; init; } = [];

    private Hint() { }

    public static Hint Create(Guid challengeId, string content, int deduction)
    {
        return new Hint
        {
            Id = Guid.CreateVersion7(),
            ChallengeId = challengeId,
            Content = content,
            Deduction = deduction,
        };
    }
}
