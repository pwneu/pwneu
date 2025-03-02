namespace Pwneu.Api.Contracts;

public record CategoryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public int ChallengesCount { get; set; }
}

public record CreateCategoryRequest
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
}

public record UserCategoryEvaluationResponse
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = default!;
    public int TotalChallenges { get; set; }
    public int TotalSolves { get; set; }
    public int IncorrectAttempts { get; set; }
    public int HintsUsed { get; set; }
}
