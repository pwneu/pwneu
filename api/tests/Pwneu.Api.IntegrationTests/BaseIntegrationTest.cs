using Bogus;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
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
    protected ApplicationDbContext DbContext { get; }
    protected Faker F { get; } = new();

    public async Task InitializeAsync()
    {
        await DbContext.Database.EnsureCreatedAsync();

        var user = new User { UserName = "test" };
        var userManager = _scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var createUser = userManager.CreateAsync(user, Constants.DefaultAdminPassword).GetAwaiter().GetResult();
        var addRole = userManager.AddToRoleAsync(user, Constants.Roles.User).GetAwaiter().GetResult();

        if (!createUser.Succeeded || !addRole.Succeeded)
            throw new InvalidOperationException("Cannot create test user");

        TestUser = userManager.FindByNameAsync("test").GetAwaiter().GetResult()
                   ?? throw new InvalidOperationException($"Cannot get test user");
    }

    public async Task DisposeAsync() => await DbContext.Database.EnsureDeletedAsync();
}