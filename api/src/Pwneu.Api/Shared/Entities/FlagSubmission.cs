using Pwneu.Api.Shared.Contracts;

namespace Pwneu.Api.Shared.Entities;

public class FlagSubmission
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid ChallengeId { get; set; }
    public string Value { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public FlagStatus FlagStatus { get; set; }
    public Challenge Challenge { get; set; } = null!;
    public User User { get; set; } = null!;
}