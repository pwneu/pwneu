using Bogus;
using FluentAssertions;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Entities;
using Pwneu.Api.Features.BlacklistedEmails;

namespace Pwneu.IntegrationTests.Features.BlacklistedEmails;

[Collection(nameof(IntegrationTestCollection))]
public class AddEmailToBlacklistTests(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotAddToBlacklist_WhenCommandIsNotValid()
    {
        // Arrange
        var faker = new Faker();
        var invalidEmails = new List<string>
        {
            "", // Empty email
            "invalid-email", // Missing @ and domain
            faker.Random.String2(10, "abcdefghijklmnopqrstuvwxyz"), // Random string without @
        };

        var addEmailToBlacklist = invalidEmails
            .Select(email => new AddEmailToBlacklist.Command(email))
            .ToList();

        // Act
        // Act
        var addEmailToBlacklistResults = new List<Result>();

        foreach (var invalidBlacklist in addEmailToBlacklist)
        {
            addEmailToBlacklistResults.Add(await Sender.Send(invalidBlacklist));
        }

        // Assert
        foreach (var addEmailToBlacklistResult in addEmailToBlacklistResults)
        {
            addEmailToBlacklistResult.Should().BeOfType<Result>();
            addEmailToBlacklistResult.IsSuccess.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Handle_Should_AddToBlacklist_WhenCommandIsValid()
    {
        // Arrange
        var addEmailToBlacklist = new AddEmailToBlacklist.Command(F.Internet.Email());

        // Act
        var addEmailToBlacklistResult = await Sender.Send(addEmailToBlacklist);

        // Assert
        addEmailToBlacklistResult.Should().BeOfType<Result>();
        addEmailToBlacklistResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_InvalidateCache_WhenNewBlacklistedEmailWasAdded()
    {
        // Arrange
        var addEmailToBlacklist = new AddEmailToBlacklist.Command(F.Internet.Email());
        await Cache.SetAsync(
            CacheKeys.BlacklistedEmails(),
            new List<IEnumerable<BlacklistedEmail>>()
        );

        // Act
        await Sender.Send(addEmailToBlacklist);

        // Assert
        var categoriesCache = await Cache.GetOrDefaultAsync<List<CategoryResponse>>(
            CacheKeys.BlacklistedEmails()
        );
        categoriesCache.Should().BeNull();
    }
}
