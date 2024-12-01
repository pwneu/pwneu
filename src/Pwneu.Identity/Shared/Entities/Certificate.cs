using System.ComponentModel.DataAnnotations;

namespace Pwneu.Identity.Shared.Entities;

public class Certificate
{
    public Guid Id { get; init; }
    [MaxLength(36)] public string UserId { get; init; } = string.Empty;
    [MaxLength(100)] public string FileName { get; init; } = string.Empty;
    [MaxLength(100)] public string ContentType { get; init; } = string.Empty;
    public byte[] Data { get; init; } = null!;
    public User? User { get; init; }
}