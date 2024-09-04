using System.ComponentModel.DataAnnotations;

namespace Pwneu.Play.Shared.Entities;

public class Submission
{
    public Guid Id { get; init; }
    [MaxLength(36)] public string UserId { get; init; } = string.Empty;
    [MaxLength(256)] public string UserName { get; init; } = string.Empty;
    public Guid ChallengeId { get; init; }
    [MaxLength(100)] public string Flag { get; init; } = string.Empty;
    public DateTime SubmittedAt { get; init; }
    public bool IsCorrect { get; init; }
    public Challenge Challenge { get; init; } = null!;
}