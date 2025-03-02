using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Entities;

public sealed class HintUsage
{
    public int Id { get; init; }

    [MaxLength(36)]
    public required string UserId { get; init; } = string.Empty;
    public required Guid HintId { get; init; }
    public required DateTime UsedAt { get; init; }
    public User User { get; init; } = null!;
    public Hint Hint { get; init; } = null!;

    private HintUsage() { }

    public static HintUsage Create(string userId, Guid hintId)
    {
        return new HintUsage
        {
            UserId = userId,
            HintId = hintId,
            UsedAt = DateTime.UtcNow,
        };
    }

    public static HintUsage CreateFromBuffer(HintUsageBuffer buffer)
    {
        return new HintUsage
        {
            UserId = buffer.UserId,
            HintId = buffer.HintId,
            UsedAt = buffer.UsedAt,
        };
    }
}
