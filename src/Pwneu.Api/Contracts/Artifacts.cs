namespace Pwneu.Api.Contracts;

public record ArtifactDataResponse
{
    public string FileName { get; init; } = default!;
    public string ContentType { get; init; } = default!;
    public byte[] Data { get; init; } = default!;
}

public record ArtifactResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = default!;
}
