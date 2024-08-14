namespace Pwneu.Shared.Contracts;

public record UserNotFoundResponse;

public record GetMemberRequest
{
    public string Id { get; set; } = default!;
}

public record UserStatsResponse
{
    public string Id { get; set; } = default!;
    public IEnumerable<CategoryEvalResponse> Evaluations { get; set; } = [];
}

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
}