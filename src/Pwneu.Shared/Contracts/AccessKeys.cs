namespace Pwneu.Shared.Contracts;

public record CreateAccessKeyRequest(bool CanBeReused, DateTime Expiration);

public record AccessKeyResponse(Guid Id, bool CanBeReused, DateTime Expiration);