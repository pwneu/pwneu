using Bogus;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using Pwneu.Api.Shared.Extensions;
using Pwneu.Api.Shared.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.IntegrationTests;

public abstract class BaseIntegrationTest : IAsyncLifetime
{
    private readonly IServiceScope _scope;

    protected BaseIntegrationTest(IntegrationTestsWebAppFactory factory)
    {
        _scope = factory.Services.CreateScope();
        Sender = _scope.ServiceProvider.GetRequiredService<ISender>();
        DbContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Cache = _scope.ServiceProvider.GetRequiredService<IFusionCache>();
        UserManager = _scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        AssertionOptions.AssertEquivalencyUsing(options =>
        {
            // Allow slight differences in DateTime comparisons.
            options.Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromSeconds(1)))
                .WhenTypeIs<DateTime>();
            return options;
        });
    }

    protected User TestUser { get; private set; } = null!;
    protected ISender Sender { get; }
    protected IFusionCache Cache { get; }
    protected UserManager<User> UserManager { get; }
    protected ApplicationDbContext DbContext { get; }
    protected Faker F { get; } = new();

    public async Task InitializeAsync()
    {
        await DbContext.Database.EnsureCreatedAsync();

        await _scope.ServiceProvider.SeedRolesAsync();
        await _scope.ServiceProvider.SeedAdminAsync();

        var appOptions = _scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>().Value;

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