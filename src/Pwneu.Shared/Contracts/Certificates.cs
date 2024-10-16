namespace Pwneu.Shared.Contracts;

public record CreateCertificateRequest
{
    public string UserId { get; init; } = default!;
    public string? CustomName { get; init; }
}