using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Entities;

public sealed class Audit
{
    public Guid Id { get; init; }

    [MaxLength(36)]
    public string UserId { get; init; } = string.Empty;

    [MaxLength(256)]
    public string UserName { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string Action { get; init; } = string.Empty;

    public DateTime PerformedAt { get; init; }

    private Audit() { }

    public static Audit Create(string userId, string userName, string action)
    {
        return new Audit
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = userName,
            Action = action,
            PerformedAt = DateTime.UtcNow,
        };
    }
}
