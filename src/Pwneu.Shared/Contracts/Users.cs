namespace Pwneu.Shared.Contracts;

public record UserNotFoundResponse;

public record GetMemberRequest
{
    public string Id { get; set; } = default!;
}

public record UserActivityResponse
{
    public string UserId { get; set; } = default!;
    public DateTime ActivityDate { get; set; }
    public int Score { get; set; }
}

// TODO -- Use this
public record GetMembersRequest;

public record UserEvalResponse
{
    public string Id { get; set; } = default!;
    public IEnumerable<UserCategoryEvalResponse> CategoryEvaluations { get; set; } = [];
}

// TODO -- Use this
public record UserResponses
{
    public IEnumerable<UserResponse> Users { get; set; } = [];
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

public record UserDeletedEvent
{
    public required string Id { get; init; } = default!;
}