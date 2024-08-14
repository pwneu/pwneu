using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
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

        // Only use the password in the options once or change the password everytime the app starts
        if (admin is not null)
            return;

        admin = new User { UserName = Consts.Admin.ToLower() };

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
}