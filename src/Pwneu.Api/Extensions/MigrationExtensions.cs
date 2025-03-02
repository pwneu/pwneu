using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;

namespace Pwneu.Api.Extensions;

public static class MigrationExtensions
{
    public static void ApplyMigrations(this WebApplication app)
    {
        var autoMigrate =
            bool.TryParse(
                app.Configuration[BuilderConfigurations.AppOptionsAutoMigrate],
                out var migrate
            ) && migrate;

        if (!autoMigrate)
            return;

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.Migrate();
    }
}
