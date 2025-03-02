using Bogus;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.IntegrationTests;

public abstract class BaseIntegrationTest : IDisposable
{
    protected const string PdfMimeType = "application/pdf";
    protected const string PdfFileExtension = ".pdf";

    protected BaseIntegrationTest(IntegrationTestsWebAppFactory factory)
    {
        Scope = factory.Services.CreateScope();
        Sender = Scope.ServiceProvider.GetRequiredService<ISender>();
        DbContext = Scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Cache = Scope.ServiceProvider.GetRequiredService<IFusionCache>();
        UserManager = Scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        TestUserPassword = Scope
            .ServiceProvider.GetRequiredService<IOptions<AppOptions>>()
            .Value.InitialAdminPassword;

        Task.Run(async () => TestUser = await CreateTestUserAsync()).GetAwaiter().GetResult();
    }

    private async Task<User> CreateTestUserAsync()
    {
        var existingUser = await UserManager.FindByNameAsync(CommonConstants.Unknown);
        if (existingUser is not null)
            return existingUser;

        var appOptions = Scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>().Value;

        var user = new User { UserName = CommonConstants.Unknown, Email = "pwneu@pwneu.pwneu" };
        var createUser = await UserManager.CreateAsync(user, appOptions.InitialAdminPassword);

        var addRole = await UserManager.AddToRoleAsync(user, Roles.Member);

        if (!createUser.Succeeded || !addRole.Succeeded)
            throw new InvalidOperationException("Cannot create test user");

        TestUser =
            await UserManager.FindByNameAsync(CommonConstants.Unknown)
            ?? throw new InvalidOperationException($"Cannot get test user");

        return TestUser;
    }

    protected string TestUserPassword { get; private set; }
    protected IServiceScope Scope { get; }
    protected ISender Sender { get; }
    protected IFusionCache Cache { get; }
    protected AppDbContext DbContext { get; }
    protected Faker F { get; } = new();
    protected UserManager<User> UserManager { get; }
    protected User TestUser { get; private set; } = null!;

    protected async Task<Category> AddValidCategoryToDatabaseAsync()
    {
        var category = Category.Create(F.Lorem.Word(), F.Lorem.Sentence());

        DbContext.Add(category);
        await DbContext.SaveChangesAsync();
        await Cache.RemoveAsync(CacheKeys.Categories());

        return category;
    }

    protected async Task<Challenge> AddValidChallengeToDatabaseAsync(Guid categoryId)
    {
        var challenge = Challenge.Create(
            categoryId,
            CommonConstants.Unknown,
            CommonConstants.Unknown,
            0,
            false,
            DateTime.MaxValue,
            0,
            [],
            [CommonConstants.Unknown]
        );

        DbContext.Add(challenge);
        await DbContext.SaveChangesAsync();
        await Cache.RemoveAsync(CacheKeys.Categories());

        return challenge;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Scope.Dispose();
    }
}
