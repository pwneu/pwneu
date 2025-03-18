namespace Pwneu.Api.Contracts;

public record AnnounceRequest
{
    public string Message { get; set; } = default!;
}
