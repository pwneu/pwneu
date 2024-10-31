namespace Pwneu.Shared.Contracts;

public record UserSolveResponse
{
    public Guid ChallengeId { get; set; }
    public string ChallengeName { get; set; } = default!;
    public DateTime SolvedAt { get; set; }
}

public record ChallengeSolveResponse
{
    public string UserId { get; set; } = default!;
    public string? UserName { get; set; }
    public DateTime SolvedAt { get; set; }
}

public record SolvedEvent
{
    public string UserId { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public Guid ChallengeId { get; set; }
    public string Flag { get; set; } = default!;
    public DateTime SolvedAt { get; set; }
}

public record SubmittedEvent
{
    public string UserId { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public Guid ChallengeId { get; set; }
    public string Flag { get; set; } = default!;
    public DateTime SubmittedAt { get; set; }
}