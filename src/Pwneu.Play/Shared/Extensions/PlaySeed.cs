using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;

namespace Pwneu.Play.Shared.Extensions;

public static class PlaySeed
{
    public static async Task SeedPlayConfigurationAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Check if the SubmissionsAllowed row already exists
        var submissionsAllowedConfig = await context.PlayConfigurations
            .FirstOrDefaultAsync(c => c.Key == Consts.SubmissionsAllowed);

        // Only set the value if it doesn't exist
        if (submissionsAllowedConfig is null)
            await context.SetPlayConfigurationValueAsync(Consts.SubmissionsAllowed, false);

        // Check if the SubmissionsAllowed row already exists
        var publicLeaderboardCount = await context.PlayConfigurations
            .FirstOrDefaultAsync(c => c.Key == Consts.PublicLeaderboardCount);

        // Only set the value if it doesn't exist
        if (publicLeaderboardCount is null)
            await context.SetPlayConfigurationValueAsync(Consts.PublicLeaderboardCount, 10);
    }
}