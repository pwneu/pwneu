using System.ComponentModel.DataAnnotations;
using Pwneu.Shared.Contracts;

namespace Pwneu.Api.Shared.Entities;

public class FlagSubmission
{
    public Guid Id { get; init; }
    [MaxLength(36)] public string UserId { get; init; } = string.Empty;
    public Guid ChallengeId { get; init; }
    [MaxLength(100)] public string Value { get; init; } = string.Empty;
    public DateTime SubmittedAt { get; init; }
    public FlagStatus FlagStatus { get; init; }
    public Challenge Challenge { get; init; } = null!;
    public User User { get; init; } = null!;
}