using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions.Entities;
using System.Threading.Channels;

namespace Pwneu.Api.Features.PointsActivities;

public static class RecalculateLeaderboards
{
    public static readonly Error NotAllowed = new(
        "RecalculateLeaderboards.NotAllowed",
        "Not allowed to recalculte leaderboards when submissions are allowed"
    );

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapDelete(
                    "play/leaderboards/recalculate",
                    async (Channel<RecalculateRequest> channel, AppDbContext context) =>
                    {
                        var submissionsAllowed = await context.CheckIfSubmissionsAllowedAsync();

                        if (submissionsAllowed)
                            return Results.BadRequest(NotAllowed);

                        await channel.Writer.WriteAsync(new RecalculateRequest());
                        return Results.NoContent();
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .RequireRateLimiting(RateLimitingPolicies.OnceEveryMinute)
                .WithTags(nameof(PointsActivities));
        }
    }
}
