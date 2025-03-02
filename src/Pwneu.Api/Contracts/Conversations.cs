namespace Pwneu.Api.Contracts;

public record ConversationResponse
{
    public int Id { get; init; }
    public string UserId { get; init; } = default!;
    public string Input { get; init; } = default!;
    public string Output { get; init; } = default!;
    public DateTime RequestedAt { get; init; }
}

public record CreateConversationRequest
{
    public string Input { get; init; } = default!;
}
