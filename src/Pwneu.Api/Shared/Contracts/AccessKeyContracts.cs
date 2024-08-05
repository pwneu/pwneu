namespace Pwneu.Api.Shared.Contracts;

public record CreateAccessKeyRequest(string Key, bool CanBeReused, DateTime Expiration);
public record AccessKeyResponse(Guid Id, string Key, bool CanBeReused, DateTime Expiration);
