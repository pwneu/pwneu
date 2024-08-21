using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Entities;

namespace Pwneu.Identity.Workers;

/// <summary>
/// A background service for cleaning up unverified emails.
/// </summary>
/// <param name="serviceProvider">Service provider.</param>
/// <param name="logger">Logger.</param>
public class UserCleanupService(IServiceProvider serviceProvider, ILogger<UserCleanupService> logger)
    : BackgroundService
{
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupUnconfirmedAccountsAsync(stoppingToken);
            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }

    private async Task CleanupUnconfirmedAccountsAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Started deleting unconfirmed users");
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var users = await userManager.Users
            .Where(u => !u.EmailConfirmed && u.CreatedAt < DateTime.UtcNow.AddHours(-24))
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            var result = await userManager.DeleteAsync(user);
            if (result.Succeeded)
                logger.LogInformation("Deleted unconfirmed user: {email}", user.Email);
            else
                logger.LogError("Failed to delete unconfirmed user: {email}", user.Email);
        }
    }
}