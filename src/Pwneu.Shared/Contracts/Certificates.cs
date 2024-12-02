namespace Pwneu.Shared.Contracts;

public record CertificateResponse
{
    public string FileName { get; init; } = default!;
    public string ContentType { get; init; } = default!;
    public byte[] Data { get; init; } = default!;
}

public enum CertificateStatus
{
    WithCertificate,
    WithoutCertificate,
    NotAllowed
}