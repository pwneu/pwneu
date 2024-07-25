using Microsoft.AspNetCore.Identity;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.Shared.Extensions;

public static class AppSeed
{
    public static async Task SeedRolesAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        string[] roleNames = [Constants.Member, Constants.Manager, Constants.Admin];

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

        var password = Environment.GetEnvironmentVariable(Constants.AdminPassword) ?? Constants.DefaultAdminPassword;

        var admin = await userManager.FindByNameAsync(Constants.Admin);

        // TODO -- Decide if only use the password in the env once or change the password everytime the application starts
        if (admin is not null)
            return;

        admin = new User { UserName = Constants.Admin.ToLower() };

        var createAdmin = await userManager.CreateAsync(admin, password);
        if (!createAdmin.Succeeded)
            throw new InvalidOperationException(
                "Failed to create admin: " +
                string.Join(", ", createAdmin.Errors.Select(e => e.Description)));

        var addManagerAdminRoles =
            await userManager.AddToRolesAsync(admin, [Constants.Admin, Constants.Manager]);
        if (!addManagerAdminRoles.Succeeded)
            throw new InvalidOperationException(
                "Failed to add admin role: " +
                string.Join(", ", addManagerAdminRoles.Errors.Select(e => e.Description)));
    }
}