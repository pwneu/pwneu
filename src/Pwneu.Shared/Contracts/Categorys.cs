namespace Pwneu.Shared.Contracts;

public record CategoryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public IEnumerable<ChallengeResponse> Challenges { get; set; } = [];
}

public record CategoryEvalResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public int TotalChallenges { get; set; }
    public int TotalSolves { get; set; }
    public int IncorrectAttempts { get; set; }
}

public record CreateCategoryRequest
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
}

public record UpdateCategoryRequest
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
}