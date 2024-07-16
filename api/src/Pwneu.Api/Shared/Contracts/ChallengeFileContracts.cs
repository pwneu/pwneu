namespace Pwneu.Api.Shared.Contracts;

public record ChallengeFileDataResponse(string FileName, string ContentType, byte[] Data);

public record ChallengeFileResponse(Guid Id, string FileName);