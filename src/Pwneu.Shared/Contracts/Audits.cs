namespace Pwneu.Shared.Contracts;

public record AuditResponse
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string Action { get; set; } = null!;
    public DateTime PerformedAt { get; set; }
}