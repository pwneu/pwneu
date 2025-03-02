namespace Pwneu.Api.Contracts;

public record CreateChallengeRequest
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public int Points { get; set; }
    public bool DeadlineEnabled { get; set; }
    public DateTime Deadline { get; set; }
    public int MaxAttempts { get; set; }
    public IEnumerable<string> Tags { get; set; } = [];
    public IEnumerable<string> Flags { get; set; } = [];
}

public record ChallengeResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public int Points { get; set; }
    public bool DeadlineEnabled { get; set; }
    public DateTime Deadline { get; set; }
    public int SolveCount { get; set; }
}

public record ChallengeDetailsResponse
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public int Points { get; set; }
    public bool DeadlineEnabled { get; set; }
    public DateTime Deadline { get; set; }
    public int MaxAttempts { get; set; }
    public int SolveCount { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> Flags { get; set; } = [];
    public IEnumerable<ArtifactResponse> Artifacts { get; set; } = [];
    public IEnumerable<HintResponse> Hints { get; set; } = [];
}

public record ChallengeDetailsNoFlagResponse
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public int Points { get; set; }
    public bool DeadlineEnabled { get; set; }
    public DateTime Deadline { get; set; }
    public int MaxAttempts { get; set; }
    public int SolveCount { get; set; }
    public List<string> Tags { get; set; } = [];
    public IEnumerable<ArtifactResponse> Artifacts { get; set; } = [];
    public IEnumerable<HintResponse> Hints { get; set; } = [];
}

public record UpdateChallengeRequest
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public int Points { get; set; }
    public bool DeadlineEnabled { get; set; }
    public DateTime Deadline { get; set; }
    public int MaxAttempts { get; set; }
    public IEnumerable<string> Tags { get; set; } = [];
    public IEnumerable<string> Flags { get; set; } = [];
}
