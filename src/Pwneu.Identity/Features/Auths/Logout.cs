using Pwneu.Shared.Common;

namespace Pwneu.Identity.Features.Auths;

public static class Logout
{
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("logout", (HttpContext httpContext) =>
                {
                    if (!httpContext.Request.Cookies.ContainsKey(Consts.RefreshToken))
                        return Results.NoContent();

                    var cookieOptions = new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddDays(-1),
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict
                    };

                    httpContext.Response.Cookies.Append(Consts.RefreshToken, string.Empty, cookieOptions);

                    return Results.NoContent();
                })
                .RequireAuthorization()
                .WithTags(nameof(Auths));
        }
    }
}