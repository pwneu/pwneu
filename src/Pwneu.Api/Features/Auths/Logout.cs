using Pwneu.Api.Common;
using Pwneu.Api.Constants;

namespace Pwneu.Api.Features.Auths;

public static class Logout
{
    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "identity/logout",
                    (HttpContext httpContext) =>
                    {
                        if (!httpContext.Request.Cookies.ContainsKey(CommonConstants.RefreshToken))
                            return Results.NoContent();

                        var cookieOptions = new CookieOptions
                        {
                            Expires = DateTimeOffset.UtcNow.AddDays(-1),
                            HttpOnly = true,
                            Secure = true,
                            SameSite = SameSiteMode.Strict,
                        };

                        httpContext.Response.Cookies.Append(
                            CommonConstants.RefreshToken,
                            string.Empty,
                            cookieOptions
                        );

                        return Results.NoContent();
                    }
                )
                .RequireAuthorization()
                .WithTags(nameof(Auths));
        }
    }
}
