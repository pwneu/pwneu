namespace Pwneu.Api.Shared.Common;

public static class Envs
{
    public static string JwtIssuer() =>
        Environment.GetEnvironmentVariable("JWT_ISSUER") ?? throw new InvalidOperationException();

    public static string JwtAudience() =>
        Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? throw new InvalidOperationException();

    public static string JwtSigningKey() =>
        Environment.GetEnvironmentVariable("JWT_SIGNING_KEY") ?? throw new InvalidOperationException();

    public static string AdminPassword() =>
        Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "PwneuPwneu!1";
}
