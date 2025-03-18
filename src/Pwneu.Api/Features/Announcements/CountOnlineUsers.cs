using Pwneu.Api.Common;
using Pwneu.Api.Constants;

namespace Pwneu.Api.Features.Announcements;

public static class CountOnlineUsers
{
    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "announcements/count",
                    () =>
                    {
                        return Results.Ok(
                            new { ConnectedUsers = AnnouncementHub.GetConnectedUserCount() }
                        );
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .WithTags(nameof(Announcements));
        }
    }
}
