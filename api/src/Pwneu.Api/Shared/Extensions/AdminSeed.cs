using Microsoft.AspNetCore.Identity;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.Shared.Extensions;

public static class AdminSeed
{
    public static async Task SeedAdminAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var userName = Constants.Roles.Admin.ToLower();
        var password = Environment.GetEnvironmentVariable(Env.AdminPassword) ?? Constants.DefaultAdminPassword;

        var admin = await userManager.FindByNameAsync(userName);

        // TODO -- Decide if only use the password in the env once or change the password everytime the application starts
        if (admin is not null)
            return;
        // {
        //     var changePassword = await userManager.ChangePasswordAsync(admin, admin.PasswordHash!, password);
        //     if (!changePassword.Succeeded)
        //         throw new InvalidOperationException(
        //             "Failed to update the admin password: " +
        //             string.Join(", ", changePassword.Errors.Select(e => e.Description)));
        //     return;
        // }

        admin = new User { UserName = userName };

        var createAdmin = await userManager.CreateAsync(admin, password);
        if (!createAdmin.Succeeded)
            throw new InvalidOperationException(
                "Failed to create admin: " +
                string.Join(", ", createAdmin.Errors.Select(e => e.Description)));

        var addAdminRole = await userManager.AddToRoleAsync(admin, Constants.Roles.Admin);
        if (!addAdminRole.Succeeded)
            throw new InvalidOperationException(
                "Failed to add admin role: " +
                string.Join(", ", addAdminRole.Errors.Select(e => e.Description)));
    }
}