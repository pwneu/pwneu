namespace Pwneu.Shared.Common;

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

    public const string AppOptionsRequireEmailVerification = "AppOptions:RequireEmailVerification";

    public const string Postgres = "Postgres";
    public const string Redis = "Redis";
    public const string Sqlite = "Sqlite";

    public const int GmailSmtpPort = 587;

    public const string RefreshToken = "refreshToken";
    public const string AntiEmailAbuse = "antiEmailAbuse";
    public const string Fixed = "fixed";

    public const string SubmissionsAllowed = "SubmissionsAllowed";
}