using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pwneu.Identity.Features.Users;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Identity.Shared.Options;
using Pwneu.Shared.Common;

namespace Pwneu.Identity.Workers;

/// <summary>
/// A background service for cleaning up unverified emails.
/// </summary>
/// <param name="serviceProvider">Service provider.</param>
/// <param name="logger">Logger.</param>
public class UserCleanupService(
    IServiceProvider serviceProvider,
    ILogger<UserCleanupService> logger,
    ISender sender,
    IOptions<AppOptions> appOptions)
    : BackgroundService
{
    private readonly AppOptions _appOptions = appOptions.Value;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_appOptions.RequireEmailVerification)
                await CleanupUnconfirmedAccountsAsync(stoppingToken);
            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }

    private async Task CleanupUnconfirmedAccountsAsync(CancellationToken cancellationToken)
    {
        if (!_appOptions.RequireEmailVerification)
            return;

        logger.LogInformation("Started deleting unconfirmed users");
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var users = await userManager.Users
            .Where(u => !u.EmailConfirmed && u.CreatedAt < DateTime.UtcNow.AddHours(-24))
            .ToListAsync(cancellationToken);

        // If admin is null we're screwed (someone tampered with the database directly).
        // But it should've been created at this point.
        var admin = await userManager.FindByNameAsync(Consts.Admin);

        foreach (var user in users)
        {
            var deleteUser = new DeleteUser.Command(user.Id, admin?.Id ?? Guid.NewGuid().ToString());

            var result = await sender.Send(deleteUser, cancellationToken);

            if (result.IsSuccess)
                logger.LogInformation("Deleted unconfirmed user: {email}", user.Email);
            else
                logger.LogError("Failed to delete unconfirmed user: {email}", user.Email);
        }
    }
}