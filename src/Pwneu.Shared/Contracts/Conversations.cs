namespace Pwneu.Shared.Contracts;

public class ConversationResponse
{
    public Guid Id { get; init; }
    public string UserId { get; init; } = default!;
    public string Input { get; init; } = default!;
    public string Output { get; init; } = default!;
    public DateTime RequestedAt { get; init; }
}