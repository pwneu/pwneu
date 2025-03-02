using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Options;

public class ChatOptions
{
    [Required]
    public bool ConversationIsEnabled { get; init; }

    [Required]
    public int MaxConversationsPerDay { get; init; }
}
