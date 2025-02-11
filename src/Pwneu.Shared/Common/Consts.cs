namespace Pwneu.Shared.Common;

/// <summary>
/// Constant strings for preventing typos.
/// </summary>
public static class Consts
{
    public const string Member = "Member";
    public const string Manager = "Manager";
    public const string Admin = "Admin";

    public const string MemberOnly = "MemberOnly";
    public const string ManagerAdminOnly = "ManagerAdminOnly";
    public const string AdminOnly = "AdminOnly";

    public const string MessageBrokerHost = "MessageBroker:Host";
    public const string MessageBrokerUsername = "MessageBroker:Username";
    public const string MessageBrokerPassword = "MessageBroker:Password";

    public const string JwtOptionsIssuer = "JwtOptions:Issuer";
    public const string JwtOptionsAudience = "JwtOptions:Audience";
    public const string JwtOptionsSigningKey = "JwtOptions:SigningKey";

    public const string ChatOptionsOpenAiApiKey = "ChatOptions:OpenAiApiKey";

    public const string Postgres = "Postgres";
    public const string Redis = "Redis";

    public const string IdentitySchema = "identity";
    public const string PlaySchema = "play";
    public const string ChatSchema = "chat";

    public const string RefreshToken = "refreshToken";
    public const string AntiEmailAbuse = "antiEmailAbuse";
    public const string Fixed = "fixed";
    public const string Challenges = "challenges";
    public const string Download = "download";
    public const string GetUsers = "getUsers";
    public const string UseHint = "useHint";
    public const string Registration = "registration";
    public const string VerifyEmail = "verifyEmail";
    public const string ResetPassword = "resetPassword";
    public const string IdentityGenerate = "identityGenerate";
    public const string PlayGenerate = "playGenerate";
    public const string ChangePassword = "changePassword";
    public const string Conversation = "conversation";

    public const string SubmissionsAllowed = "SubmissionsAllowed";
    public const string PublicLeaderboardCount = "PublicLeaderboardCount";
    public const string ChallengesLocked = "ChallengesLocked";

    public const string IsCertificationEnabled = "IsCertificationEnabled";
    public const string IsTurnstileEnabled = "IsTurnstileEnabled";

    public const string TurnstileChallengeUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
    public const string CfConnectingIp = "Cf-Connecting-Ip";
}