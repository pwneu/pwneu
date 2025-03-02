namespace Pwneu.Api.Constants;

public static class RateLimitingPolicies
{
    public const string Fixed = "fixed";
    public const string ExpensiveRequest = "expensiveRequest";
    public const string AntiEmailAbuse = "antiEmailAbuse";
    public const string GetUsers = "getUsers";
    public const string Registration = "registration";
    public const string VerifyEmail = "verifyEmail";
    public const string ResetPassword = "resetPassword";
    public const string FileGeneration = "fileGeneration";
    public const string OnceEveryMinute = "onceEveryMinute";
    public const string GetChallenges = "getChallenges";
    public const string GetArtifact = "getArtifact";
    public const string UseHint = "useHint";
}
