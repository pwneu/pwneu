namespace Pwneu.Shared.Contracts;

public record ArtifactDataResponse(string FileName, string ContentType, byte[] Data);

public record ArtifactResponse(Guid Id, string FileName);