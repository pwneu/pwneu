using Bogus;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Identity.Shared.Extensions;
using Pwneu.Identity.Shared.Options;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.IntegrationTests;

public abstract class BaseIntegrationTest : IAsyncLifetime
{
    protected readonly IServiceScope Scope;

    protected BaseIntegrationTest(IntegrationTestsWebAppFactory factory)
    {
        Scope = factory.Services.CreateScope();
        Sender = Scope.ServiceProvider.GetRequiredService<ISender>();
        DbContext = Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Cache = Scope.ServiceProvider.GetRequiredService<IFusionCache>();
        UserManager = Scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        TestUserPassword = Scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>().Value.InitialAdminPassword;

        AssertionOptions.AssertEquivalencyUsing(options =>
        {
            // Allow slight differences in DateTime comparisons.
            options.Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromSeconds(1)))
                .WhenTypeIs<DateTime>();
            return options;
        });
    }

    protected string TestUserPassword { get; private set; }
    protected User TestUser { get; private set; } = null!;
    protected ISender Sender { get; }
    protected IFusionCache Cache { get; }
    protected UserManager<User> UserManager { get; }
    protected ApplicationDbContext DbContext { get; }
    protected Faker F { get; } = new();

    public async Task InitializeAsync()
    {
        await DbContext.Database.EnsureCreatedAsync();
        await Cache.RemoveAsync(Keys.IsCertificationEnabled());

        await Scope.ServiceProvider.SeedRolesAsync();
        await Scope.ServiceProvider.SeedAdminAsync();
        await Scope.ServiceProvider.SeedIdentityConfigurationAsync();

        var appOptions = Scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>().Value;

        var user = new User { UserName = "test" };
        var createUser = await UserManager.CreateAsync(user, appOptions.InitialAdminPassword);

        var addRole = await UserManager.AddToRoleAsync(user, Consts.Member);

        if (!createUser.Succeeded || !addRole.Succeeded)
            throw new InvalidOperationException("Cannot create test user");

        TestUser = await UserManager.FindByNameAsync("test") ??
                   throw new InvalidOperationException($"Cannot get test user");
    }

    public async Task DisposeAsync() => await DbContext.Database.EnsureDeletedAsync();
}