using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Shared.Entities;

public class AccessKey
{
    public Guid Id { get; init; }
    public bool CanBeReused { get; init; }
    public DateTime Expiration { get; init; }
}