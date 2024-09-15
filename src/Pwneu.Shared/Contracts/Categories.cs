namespace Pwneu.Shared.Contracts;

public record CategoryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
}

public record CategoryDetailsResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public IEnumerable<ChallengeResponse> Challenges { get; set; } = [];
}

public record UserCategoryEvalResponse
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = default!;
    public int TotalChallenges { get; set; }
    public int TotalSolves { get; set; }
    public int IncorrectAttempts { get; set; }
    public int HintsUsed { get; set; }
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