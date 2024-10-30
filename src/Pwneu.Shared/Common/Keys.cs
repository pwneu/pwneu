namespace Pwneu.Shared.Common;

/// <summary>
/// Keys used for caching to avoid using wrong keys.
/// </summary>
public static class Keys
{
    // Key for caching a list of all CategoryResponse.
    public static string Categories() => "categories";

    // Key for caching a list of all category Ids.
    public static string CategoryIds() => "categoryIds";

    // Key for caching access keys.
    public static string AccessKeys() => "accessKeys";

    // Key for caching a single CategoryResponse.
    public static string Category(Guid id) => $"category:{id}";

    // Key for caching a list of all challenge ids.
    public static string ChallengeIds() => "challengeIds";

    // Key for caching ChallengeDetailsResponse.
    public static string ChallengeDetails(Guid id) => $"challenge:{id}:details";

    // Key for caching ArtifactDataResponse.
    public static string ArtifactData(Guid id) => $"artifact:{id}:data";

    // Key for caching UserResponse.
    public static string User(string id) => $"user:{id}";

    // Key for caching UserDetailsResponse.
    public static string UserDetails(string id) => $"user:{id}:details";

    // Key for caching UserTokenResponse.
    public static string UserToken(string id) => $"user:{id}:token";

    // Key for storing cache of user graph.
    public static string UserGraph(string id) => $"user:{id}:graph";

    // Key for storing cache of user graph.
    public static string UserSolveIds(string id) => $"user:{id}:solves";

    // Keys for caching a list of all member ids.
    public static string MemberIds() => "memberIds";

    // Key for storing cache of active user ids.
    public static string ActiveUserIds() => "user:ids:active";

    // Key for caching UserCategoryEvalResponse.
    public static string UserCategoryEval(
        string userId,
        Guid categoryId) => $"user:{userId}:category:{categoryId}:eval";

    // Key for caching leaderboards.
    public static string UserRanks() => "userRanks";

    // Key for caching graph of top users.
    public static string TopUsersGraph() => "topUserGraph";

    // Key for caching if there's a recent calculation of leaderboard when saving a correct submission.
    public static string HasRecentLeaderboardCount() => "hasRecentLeaderboardCount";

    // Key for caching challenge flags.
    public static string Flags(Guid challengeId) => $"challenge:{challengeId}:flag";

    // Key for caching if submissions are allowed.
    public static string SubmissionsAllowed() => "submissionsAllowed";

    // Key for caching if certification is enabled.
    public static string IsCertificationEnabled() => "isCertificationEnabled";

    // Key for caching the issuer of the certificate.
    public static string CertificationIssuer() => "certificationIssuer";

    // Key for caching if turnstile is enabled.
    public static string IsTurnstileEnabled() => "isTurnstileEnabled";

    // Key for caching if how many users in leaderboards are visible.
    public static string PublicLeaderboardCount() => "publicLeaderboardCount";

    // Key for caching if the user has already solved the challenge.
    public static string HasSolved(string userId, Guid challengeId) => $"hasSolved:{userId}:{challengeId}";

    // Key for caching the count of the user's recent submissions.
    public static string RecentSubmits(string userId, Guid challengeId) => $"recentSubmits:{userId}:{challengeId}";

    // Key for caching the number of attempts left by the user.
    public static string AttemptsLeft(string userId, Guid challengeId) => $"attemptsLeft:{userId}:{challengeId}";
    public static string FailedLoginCount(string ipAddress) => $"failedLoginCount:{ipAddress}";
}