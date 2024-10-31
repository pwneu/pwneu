using System.ComponentModel.DataAnnotations;

namespace Pwneu.Play.Shared.Entities;

public class Solve
{
    public Guid Id { get; init; }
    [MaxLength(36)] public string UserId { get; init; } = string.Empty;
    [MaxLength(256)] public string UserName { get; init; } = string.Empty;
    public Guid ChallengeId { get; init; }
    [MaxLength(100)] public string Flag { get; init; } = string.Empty;
    public DateTime SolvedAt { get; init; }
    public Challenge Challenge { get; init; } = null!;
}