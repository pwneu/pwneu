using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Entities;

public sealed class Artifact
{
    public Guid Id { get; private init; }
    public Guid ChallengeId { get; private init; }

    [MaxLength(100)]
    public string FileName { get; private init; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; private init; } = string.Empty;
    public byte[] Data { get; private init; } = null!;
    public Challenge Challenge { get; private init; } = null!;

    private Artifact() { }

    public static Artifact Create(
        Guid challengeId,
        string fileName,
        string contentType,
        byte[] data
    )
    {
        return new Artifact
        {
            Id = Guid.CreateVersion7(),
            ChallengeId = challengeId,
            FileName = fileName,
            ContentType = contentType,
            Data = data,
        };
    }
}
