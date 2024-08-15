namespace Pwneu.Shared.Common;

/// <summary>
/// Keys used for caching to avoid using wrong keys.
/// </summary>
public static class Keys
{
    public static string Categories() => "categories";
    public static string CategoryIds() => "categoryIds"; // TODO -- Invalidate cache
    public static string AccessKeys() => "accessKeys";
    public static string Category(Guid id) => $"category:{id}";
    public static string Challenge(Guid id) => $"challenge:{id}";
    public static string Artifact(Guid id) => $"artifact:{id}";
    public static string User(string id) => $"user:{id}";
    public static string UserDetails(string id) => $"user:{id}:details";
    public static string UserEval(string id) => $"user:{id}:eval";

    public static string UserCategoryEval(
        string userId,
        Guid categoryId) => $"user:{userId}:category:{categoryId}:eval";

    public static string Flags(Guid challengeId) => $"challenge:{challengeId}:flag";
    public static string Hint(Guid id) => $"hint{id}";
    public static string Hints(Guid challengeId) => $"challenge:{challengeId}:hint";
    public static string HasSolved(string userId, Guid challengeId) => $"hasSolved:{userId}:{challengeId}";
    public static string RecentSubmits(string userId, Guid challengeId) => $"recentSubmits:{userId}:{challengeId}";
    public static string AttemptsLeft(string userId, Guid challengeId) => $"attemptsLeft:{userId}:{challengeId}";
}