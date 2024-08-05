namespace Pwneu.Api.Shared.Common;

/// <summary>
/// Keys used for caching to avoid using wrong keys.
/// </summary>
public static class Keys
{
    public static string Categories() => "category";
    public static string AccessKeys() => "accessKeys";
    public static string Category(Guid id) => $"category:{id}";
    public static string Challenge(Guid id) => $"challenge:{id}";
    public static string Artifact(Guid id) => $"artifact:{id}";
    public static string User(string id) => $"user:{id}";
    public static string UserStats(string id) => $"user:{id}:stats";
    public static string Flags(Guid challengeId) => $"flag:{challengeId}";
}