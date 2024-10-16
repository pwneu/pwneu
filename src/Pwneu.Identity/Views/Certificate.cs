namespace Pwneu.Identity.Views;

public class Certificate
{
    public required string UserId { get; init; }
    public required string FullName { get; init; }
    public required DateTime IssuedAt { get; init; }
    public required string CertificationIssuer { get; init; }
}