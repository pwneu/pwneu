using System.ComponentModel.DataAnnotations;

namespace Pwneu.Chat.Shared.Entities;

public class Conversation
{
    public Guid Id { get; init; }
    [MaxLength(36)] public string UserId { get; init; } = string.Empty;
    [MaxLength(2000)] public string Input { get; init; } = string.Empty;
    [MaxLength(4000)] public string Output { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; }
}