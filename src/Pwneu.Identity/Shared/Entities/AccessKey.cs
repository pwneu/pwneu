namespace Pwneu.Identity.Shared.Entities;

// TODO -- Create access key for managers

public class AccessKey
{
    public Guid Id { get; init; }
    public bool CanBeReused { get; init; }
    public DateTime Expiration { get; init; }
}