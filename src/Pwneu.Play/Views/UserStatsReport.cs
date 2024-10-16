using Pwneu.Shared.Contracts;

namespace Pwneu.Play.Views;

public class UserStatsReport
{
    public string Id { get; init; } = default!;
    public string? UserName { get; init; }
    public string? FullName { get; init; }
    public int? Position { get; init; }
    public int? Points { get; init; }
    public List<UserCategoryEvalResponse> CategoryEvaluations { get; init; } = [];
    public List<UserActivityResponse> UserGraph { get; init; } = [];
    public DateTime IssuedAt { get; init; }
}