namespace Pwneu.Shared.Contracts;

public record ChallengeResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
}

public record ChallengeDetailsResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public int Points { get; set; }
    public bool DeadlineEnabled { get; set; }
    public DateTime Deadline { get; set; }
    public int MaxAttempts { get; set; }
    public int SolveCount { get; set; }
    public IEnumerable<ArtifactResponse> Artifacts { get; set; } = [];
}

public record CreateChallengeRequest
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public int Points { get; set; }
    public bool DeadlineEnabled { get; set; }
    public DateTime Deadline { get; set; }
    public int MaxAttempts { get; set; }
    public IEnumerable<string> Flags { get; set; } = [];
}

public record UpdateChallengeRequest
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public int Points { get; set; }
    public bool DeadlineEnabled { get; set; }
    public DateTime Deadline { get; set; }
    public int MaxAttempts { get; set; }
    public IEnumerable<string> Flags { get; set; } = [];
}