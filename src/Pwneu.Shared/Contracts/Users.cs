namespace Pwneu.Shared.Contracts;

public record UserNotFoundResponse;

public record UserDetailsNotFoundResponse;

public record GetMemberIdsRequest;

public record MemberIdsResponse
{
    public List<string> MemberIds { get; set; } = [];
}

public record GetMemberRequest
{
    public string Id { get; set; } = default!;
}

public record GetMemberDetailsRequest
{
    public string Id { get; set; } = default!;
}

public record UserActivityResponse
{
    public string UserId { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public DateTime ActivityDate { get; set; }
    public int Score { get; set; }
}

public record UserEvalResponse
{
    public string Id { get; set; } = default!;
    public IEnumerable<UserCategoryEvalResponse> CategoryEvaluations { get; set; } = [];
}

public record UserResponse
{
    public string Id { get; set; } = default!;
    public string? UserName { get; set; }
}

public record UserTokenResponse
{
    public string? RefreshToken { get; set; } = default!;
    public DateTime RefreshTokenExpiry { get; set; } = default!;
}

public record UserInfoResponse
{
    public string Id { get; set; } = default!;
    public string? UserName { get; set; }
    public List<string> Roles { get; set; } = [];
}

public record UserDetailsResponse
{
    public string Id { get; set; } = default!;
    public string? UserName { get; set; }
    public string FullName { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public string? Email { get; set; }
    public bool EmailConfirmed { get; set; }
    public List<string> Roles { get; set; } = [];
}

public record UserDeletedEvent
{
    public required string Id { get; init; } = default!;
}

public record UsersGraphResponse
{
    public List<List<UserActivityResponse>> UsersGraph { get; set; } = [];
    public List<DateTime> GraphLabels { get; set; } = [];
}

public record LeaderboardsResponse
{
    public UserRankResponse? RequesterRank { get; set; } = default!;
    public List<UserRankResponse> UserRanks { get; set; } = [];
    public UsersGraphResponse TopUsersGraph { get; set; } = default!;
    public bool RequesterIsMember { get; set; }
    public int PublicLeaderboardCount { get; set; }
    public int TotalLeaderboardCount { get; set; }
}

public record UserRankResponse
{
    public string Id { get; set; } = default!;
    public string? UserName { get; set; } = default!;
    public int Position { get; set; }
    public int Points { get; set; }
    public DateTime LatestSolve { get; set; }
}