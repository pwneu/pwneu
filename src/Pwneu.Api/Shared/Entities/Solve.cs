using System.ComponentModel.DataAnnotations;

namespace Pwneu.Api.Shared.Entities;

public class Solve
{
    [MaxLength(36)] public string UserId { get; init; } = string.Empty;
    public Guid ChallengeId { get; init; }
    public User User { get; init; } = null!;
    public Challenge Challenge { get; init; } = null!;
    public DateTime SolvedAt { get; init; }
}