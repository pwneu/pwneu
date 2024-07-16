namespace Pwneu.Api.Entities;

public class ChallengeFile
{
    public Guid Id { get; set; }
    public Guid ChallengeId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public byte[] Data { get; set; } = null!;
    public Challenge Challenge { get; set; } = null!;
}