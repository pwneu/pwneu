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

        // Check if the configuration already exists
        var submissionsAllowedConfig = await context.PlayConfigurations
            .FirstOrDefaultAsync(c => c.Key == Consts.SubmissionsAllowed);

        // Only set the value if it doesn't exist
        if (submissionsAllowedConfig is null)
            await context.SetPlayConfigurationValueAsync(Consts.SubmissionsAllowed, false);
    }
}