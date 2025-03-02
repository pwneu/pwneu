using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Pwneu.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static T? GetLoggedInUserId<T>(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var loggedInUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (typeof(T) == typeof(string))
        {
            return (T?)Convert.ChangeType(loggedInUserId, typeof(T));
        }

        if (typeof(T) == typeof(int) || typeof(T) == typeof(long))
        {
            return loggedInUserId != null
                ? (T)Convert.ChangeType(loggedInUserId, typeof(T))
                : (T)Convert.ChangeType(0, typeof(T));
        }

        throw new Exception("Invalid type provided");
    }

    public static string? GetLoggedInUserName(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindFirstValue(JwtRegisteredClaimNames.Name);
    }

    public static string? GetLoggedInUserEmail(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindFirstValue(JwtRegisteredClaimNames.Email);
    }

    public static IEnumerable<string> GetRoles(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindAll(ClaimTypes.Role).Select(c => c.Value);
    }
}
