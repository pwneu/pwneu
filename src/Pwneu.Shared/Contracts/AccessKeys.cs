namespace Pwneu.Shared.Contracts;

public record CreateAccessKeyRequest
{
    public bool CanBeReused { get; set; }
    public DateTime Expiration { get; set; }
}

public record AccessKeyResponse
{
    public Guid Id { get; set; }
    public bool CanBeReused { get; set; }
    public DateTime Expiration { get; set; }
}