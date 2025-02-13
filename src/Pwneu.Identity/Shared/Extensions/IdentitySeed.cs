using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Identity.Shared.Options;
using Pwneu.Shared.Common;

namespace Pwneu.Identity.Shared.Extensions;

public static class IdentitySeed
{
    public static async Task SeedRolesAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        string[] roleNames = [Consts.Member, Consts.Manager, Consts.Admin];

        foreach (var roleName in roleNames)
        {
            if (await roleManager.RoleExistsAsync(roleName)) continue;

            var createRoleResult = await roleManager.CreateAsync(new IdentityRole(roleName));

            if (!createRoleResult.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to add {roleName}" +
                    string.Join(", ", createRoleResult.Errors.Select(e => e.Description)));
        }
    }

    public static async Task SeedAdminAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var appOptions = scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>().Value;

        var password = appOptions.InitialAdminPassword;

        var admin = await userManager.FindByNameAsync(Consts.Admin);

        if (admin is not null)
        {
            // Always reset the refresh token of the admin.
            admin.RefreshToken = null;
            admin.RefreshTokenExpiry = DateTime.MinValue;
            var updateAdmin = await userManager.UpdateAsync(admin);
            if (!updateAdmin.Succeeded)
                throw new InvalidOperationException(
                    "Failed to reset admin refresh token: " +
                    string.Join(", ", updateAdmin.Errors.Select(e => e.Description)));

            // Change the password if the admin exists.
            var token = await userManager.GeneratePasswordResetTokenAsync(admin);
            var resetPassword = await userManager.ResetPasswordAsync(admin, token, password);
            if (!resetPassword.Succeeded)
                throw new InvalidOperationException(
                    "Failed to reset admin password: " +
                    string.Join(", ", resetPassword.Errors.Select(e => e.Description)));

            return;
        }

        admin = new User { UserName = Consts.Admin.ToLower(), EmailConfirmed = true };

        var createAdmin = await userManager.CreateAsync(admin, password);
        if (!createAdmin.Succeeded)
            throw new InvalidOperationException(
                "Failed to create admin: " +
                string.Join(", ", createAdmin.Errors.Select(e => e.Description)));

        var addManagerAdminRoles =
            await userManager.AddToRolesAsync(admin, [Consts.Admin, Consts.Manager]);
        if (!addManagerAdminRoles.Succeeded)
            throw new InvalidOperationException(
                "Failed to add admin role: " +
                string.Join(", ", addManagerAdminRoles.Errors.Select(e => e.Description)));
    }

    public static async Task SeedIdentityConfigurationAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var appOptions = scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>().Value;

        await context.SetIdentityConfigurationValueAsync(Consts.IsTurnstileEnabled, appOptions.IsTurnstileEnabled);

        // Check if the IsCertificationEnabled row already exists.
        var isCertificationEnabled = await context.IdentityConfigurations
            .FirstOrDefaultAsync(c => c.Key == Consts.IsCertificationEnabled);

        // Only set the value if it doesn't exist.
        if (isCertificationEnabled is null)
            await context.SetIdentityConfigurationValueAsync(Consts.IsCertificationEnabled, false);
    }
}