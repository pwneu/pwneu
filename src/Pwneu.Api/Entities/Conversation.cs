using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Entities;

public class Conversation
{
    public int Id { get; init; }

    [MaxLength(36)]
    public string UserId { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string Input { get; init; } = string.Empty;

    [MaxLength(4000)]
    public string Output { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
    public User User { get; init; } = null!;

    private Conversation() { }

    public static Conversation Create(string userId, string input, string Output)
    {
        return new Conversation
        {
            UserId = userId,
            Input = input,
            Output = Output,
            RequestedAt = DateTime.UtcNow,
        };
    }
}
