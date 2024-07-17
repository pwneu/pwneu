namespace Pwneu.Api.Shared.Entities;

public class Solve
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid ChallengeId { get; set; }
    public User User { get; set; } = null!; // TODO: Do something about dependency loop warning
    public Challenge Challenge { get; set; } = null!; // TODO: Do something about dependency loop warning
    public DateTime SolvedAt { get; set; }
}