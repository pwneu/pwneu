namespace Pwneu.Shared.Contracts;

public record UserResponse
{
    public string Id { get; set; } = default!;
    public string? UserName { get; set; }
}

public record UserDetailsResponse
{
    public string Id { get; set; } = default!;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string FullName { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public int TotalPoints { get; set; }
    public int CorrectAttempts { get; set; }
    public int IncorrectAttempts { get; set; }
}

public record UserStatsResponse
{
    public string Id { get; set; } = default!;
    public IEnumerable<CategoryEvalResponse> Evaluations { get; set; } = [];
}