namespace Pwneu.Api.Contracts;

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
    public bool IsVisibleOnLeaderboards { get; set; }
    public List<string> Roles { get; set; } = [];
}

public record UserDetailsNoEmailResponse
{
    public string Id { get; set; } = default!;
    public string? UserName { get; set; }
    public string FullName { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool IsVisibleOnLeaderboards { get; set; }
    public List<string> Roles { get; set; } = [];
}

public record UserGraphResponse
{
    public string UserId { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public List<ActivityDataResponse> Activities { get; set; } = [];
}

public record ActivityDataResponse
{
    public int? Score { get; init; }
    public DateTime OccurredAt { get; init; }
}

public record LeaderboardsResponse
{
    public UserRankResponse? RequesterRank { get; set; } = default!;
    public List<UserRankResponse> UserRanks { get; set; } = [];
    public List<UserGraphResponse> TopUsersGraph { get; set; } = default!;
    public bool RequesterIsMember { get; set; }
    public int PublicLeaderboardCount { get; set; }
    public int TotalLeaderboardCount { get; set; }
}

public record UserRanksResponse
{
    public List<UserRankResponse> UserRanks { get; set; } = [];
    public int TotalParticipants { get; set; }
}

public record UserRankResponse
{
    public string Id { get; set; } = default!;
    public string? UserName { get; set; } = default!;
    public int Position { get; set; }
    public int Points { get; set; }
    public DateTime LatestSolve { get; set; }
}

public record UserPlayDataResponse
{
    public string Id { get; set; } = default!;
    public int TotalSolves { get; set; }
    public int TotalHintUsages { get; set; }
}

public record UserEvaluationResponse
{
    public string Id { get; set; } = default!;
    public IEnumerable<UserCategoryEvaluationResponse> CategoryEvaluations { get; set; } = [];
}
