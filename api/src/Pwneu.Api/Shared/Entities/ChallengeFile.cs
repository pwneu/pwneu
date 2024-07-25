using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Shared.Entities;

public class ChallengeFile
{
    public Guid Id { get; init; }
    public Guid ChallengeId { get; init; }
    [MaxLength(100)] public string FileName { get; init; } = string.Empty;
    [MaxLength(30)] public string ContentType { get; init; } = string.Empty;
    public byte[] Data { get; init; } = null!;
    public Challenge Challenge { get; init; } = null!;
}