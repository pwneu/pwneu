namespace Pwneu.Api.Entities;

public sealed class AccessKey
{
    public Guid Id { get; init; }
    public bool ForManager { get; init; }
    public bool CanBeReused { get; init; }
    public DateTime Expiration { get; init; }

    private AccessKey() { }

    public static AccessKey Create(bool forManager, bool canBeReused, DateTime expiration)
    {
        return new AccessKey
        {
            Id = Guid.CreateVersion7(),
            ForManager = forManager,
            CanBeReused = canBeReused,
            Expiration = expiration.ToUniversalTime(),
        };
    }
}
