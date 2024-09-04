using Microsoft.EntityFrameworkCore;
using Pwneu.Chat.Shared.Data;

namespace Pwneu.Chat.Shared.Extensions;

public static class MigrationExtensions
{
    public static void ApplyMigrations(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        context.Database.Migrate();
    }
}