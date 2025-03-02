using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Entities;

public sealed class BlacklistedEmail
{
    public Guid Id { get; init; }

    [MaxLength(100)]
    public string Email { get; init; } = string.Empty;

    private BlacklistedEmail() { }

    public static BlacklistedEmail Create(string email)
    {
        return new BlacklistedEmail { Id = Guid.CreateVersion7(), Email = email };
    }
}
