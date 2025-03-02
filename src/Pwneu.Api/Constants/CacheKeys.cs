namespace Pwneu.Api.Constants;

public static class CacheKeys
{
    // Key for caching a list of all CategoryResponse.
    public static string Categories() => "categories";

    // Key for storing cache of users solved challenges.
    public static string UserSolvedChallengeIds(string id) => $"user:{id}:solves";

    // Key for caching ChallengeDetailsResponse.
    public static string ChallengeDetails(Guid id) => $"challenge:{id}:details";

    // Key for caching ArtifactDataResponse.
    public static string ArtifactData(Guid id) => $"artifact:{id}:data";

    // Key for caching List<AccessKey>.
    public static string AccessKeys() => "accessKeys";

    // Key for caching blacklisted emails.
    public static string BlacklistedEmails() => "blacklistedEmails";

    // Key for caching UserTokenResponse.
    public static string UserToken(string id) => $"user:{id}:token";

    // Key for caching UserDetailsNoEmailResponse.
    public static string UserDetailsNoEmail(string id) => $"user:{id}:detailsNoEmail";

    // Key for caching if user exists.
    public static string UserExists(string id) => $"user:{id}:exists";

    // Key for caching how many failed logins of an IP address.
    public static string FailedLoginCountByIp(string ipAddress) => $"failedLoginCount:ip:{ipAddress}";

    // Key for caching how many failed logins of a user.
    public static string FailedLoginCountByUser(string userId) => $"failedLoginCount:user:{userId}";

    // Key for caching if submissions are allowed.
    public static string SubmissionsAllowed() => "submissionsAllowed";

    // Key for caching if certification is enabled.
    public static string IsCertificationEnabled() => "isCertificationEnabled";

    // Key for caching if turnstile is enabled.
    public static string IsTurnstileEnabled() => "isTurnstileEnabled";

    // Key for caching if how many users in leaderboards are visible.
    public static string PublicLeaderboardCount() => "publicLeaderboardCount";

    // Key for caching if challenges can be updated or deleted.
    public static string ChallengesLocked() => "challengesLocked";

    // Key for caching the number of attempts left of the user in a challenge.
    public static string UserAttemptsLeftInChallenge(string userId, Guid challengeId) =>
        $"userAttemptsLeftInChallenge:{userId}:{challengeId}";

    // Key for caching if the user has already solved the challenge.
    public static string UserHasSolvedChallenge(string userId, Guid challengeId) =>
        $"userHasSolvedChallenge:{userId}:{challengeId}";

    // Key for caching the count of the user's recent submissions.
    public static string UserRecentSubmissionCount(string userId) =>
        $"userRecentSubmissionCount:{userId}";

    // Key for caching if the user has already solved the challenge.
    public static string UserHasUsedHint(string userId, Guid hintId) =>
        $"userHasUsedHint:{userId}:{hintId}";

    // Key for caching List<UserEvaluationResponse>.
    public static string UserCategoryEvaluations(string userId) =>
        $"user:{userId}:categoryEvaluations";

    // Key for storing cache of user graph.
    public static string UserGraph(string id) => $"user:{id}:graph";

    // Key for caching leaderboards.
    public static string UserRanks() => "userRanks";

    // Key for caching UserRankResponse.
    public static string UserRank(string id) => $"user:{id}:rank";

    // Key for caching graph of top users.
    public static string TopUsersGraph() => "topUsersGraph";
}
