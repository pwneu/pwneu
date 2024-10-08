using Bogus;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.IntegrationTests;

public abstract class BaseIntegrationTest : IAsyncLifetime
{
    protected BaseIntegrationTest(IntegrationTestsWebAppFactory factory)
    {
        var scope = factory.Services.CreateScope();
        Sender = scope.ServiceProvider.GetRequiredService<ISender>();
        DbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Cache = scope.ServiceProvider.GetRequiredService<IFusionCache>();

        AssertionOptions.AssertEquivalencyUsing(options =>
        {
            // Allow slight differences in DateTime comparisons.
            options.Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromSeconds(1)))
                .WhenTypeIs<DateTime>();
            return options;
        });
    }

    protected ISender Sender { get; }

    protected IFusionCache Cache { get; }

    protected ApplicationDbContext DbContext { get; }
    protected Faker F { get; } = new();

    public async Task InitializeAsync()
    {
        await DbContext.Database.EnsureCreatedAsync();
        await DbContext.SetPlayConfigurationValueAsync(Consts.SubmissionsAllowed, true);
    }

    public async Task DisposeAsync() => await DbContext.Database.EnsureDeletedAsync();
}