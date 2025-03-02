using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Entities;

public sealed class Certificate
{
    public Guid Id { get; init; }

    [MaxLength(36)]
    public string UserId { get; init; } = string.Empty;

    [MaxLength(100)]
    public string FileName { get; init; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; init; } = string.Empty;
    public byte[] Data { get; init; } = null!;
    public User? User { get; init; }

    private Certificate() { }

    public static Certificate Create(
        string userId,
        string fileName,
        string contentType,
        byte[] data
    )
    {
        return new Certificate
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            FileName = fileName,
            ContentType = contentType,
            Data = data,
        };
    }
}
