using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Options;

namespace Pwneu.Api.Extensions;

public static class SeedExtensions
{
    public static async Task SeedRolesAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        string[] roleNames = [Roles.Member, Roles.Manager, Roles.Admin];

        foreach (var roleName in roleNames)
        {
            if (await roleManager.RoleExistsAsync(roleName))
                continue;

            var createRoleResult = await roleManager.CreateAsync(new IdentityRole(roleName));

            if (!createRoleResult.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to add {roleName}"
                        + string.Join(", ", createRoleResult.Errors.Select(e => e.Description))
                );
        }
    }

    public static async Task SeedAdminAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var appOptions = scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>().Value;
        var smtpOptions = scope.ServiceProvider.GetRequiredService<IOptions<SmtpOptions>>().Value;

        var password = appOptions.InitialAdminPassword;

        var admin = await userManager.FindByNameAsync(Roles.Admin);

        if (admin is not null)
        {
            // Always reset the refresh token and email of the admin.
            admin.RefreshToken = null;
            admin.RefreshTokenExpiry = DateTime.MinValue;
            admin.Email = smtpOptions.SenderAddress;

            var updateAdmin = await userManager.UpdateAsync(admin);
            if (!updateAdmin.Succeeded)
                throw new InvalidOperationException(
                    "Failed to reset admin refresh token: "
                        + string.Join(", ", updateAdmin.Errors.Select(e => e.Description))
                );

            // Change the password if the admin exists.
            var token = await userManager.GeneratePasswordResetTokenAsync(admin);
            var resetPassword = await userManager.ResetPasswordAsync(admin, token, password);
            if (!resetPassword.Succeeded)
                throw new InvalidOperationException(
                    "Failed to reset admin password: "
                        + string.Join(", ", resetPassword.Errors.Select(e => e.Description))
                );

            return;
        }

        admin = new User
        {
            UserName = Roles.Admin.ToLower(),
            Email = smtpOptions.SenderAddress,
            EmailConfirmed = true,
        };

        var createAdmin = await userManager.CreateAsync(admin, password);
        if (!createAdmin.Succeeded)
            throw new InvalidOperationException(
                "Failed to create admin: "
                    + string.Join(", ", createAdmin.Errors.Select(e => e.Description))
            );
        var addManagerAdminRoles = await userManager.AddToRolesAsync(
            admin,
            [Roles.Admin, Roles.Manager]
        );
        if (!addManagerAdminRoles.Succeeded)
            throw new InvalidOperationException(
                "Failed to add admin role: "
                    + string.Join(", ", addManagerAdminRoles.Errors.Select(e => e.Description))
            );
    }

    public static async Task SeedConfigurationAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var appOptions = scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>().Value;

        await context.SetConfigurationValueAsync(
            ConfigurationKeys.IsTurnstileEnabled,
            appOptions.IsTurnstileEnabled
        );

        // Check if the IsCertificationEnabled row already exists.
        var isCertificationEnabled = await context.Configurations.FirstOrDefaultAsync(c =>
            c.Key == ConfigurationKeys.IsCertificationEnabled
        );
        // Only set the value if it doesn't exist.
        if (isCertificationEnabled is null)
            await context.SetConfigurationValueAsync(
                ConfigurationKeys.IsCertificationEnabled,
                false
            );

        // Check if the SubmissionsAllowed row already exists.
        var submissionsAllowedConfig = await context.Configurations.FirstOrDefaultAsync(c =>
            c.Key == ConfigurationKeys.SubmissionsAllowed
        );
        // Only set the value if it doesn't exist.
        if (submissionsAllowedConfig is null)
            await context.SetConfigurationValueAsync(ConfigurationKeys.SubmissionsAllowed, false);

        // Check if the PublicLeaderboardCount row already exists.
        var publicLeaderboardCount = await context.Configurations.FirstOrDefaultAsync(c =>
            c.Key == ConfigurationKeys.PublicLeaderboardCount
        );
        if (publicLeaderboardCount is null)
            await context.SetConfigurationValueAsync(ConfigurationKeys.PublicLeaderboardCount, 10);

        // Check if the PublicLeaderboardCount row already exists.
        var challengesLocked = await context.Configurations.FirstOrDefaultAsync(c =>
            c.Key == ConfigurationKeys.ChallengesLocked
        );
        // Only set the value if it doesn't exist.
        if (challengesLocked is null)
            await context.SetConfigurationValueAsync(ConfigurationKeys.ChallengesLocked, false);
    }
}
