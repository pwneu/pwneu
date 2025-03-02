using FluentAssertions;
using Pwneu.Api.Constants;
using Pwneu.Api.Entities;
using Pwneu.Api.Features.BlacklistedEmails;

namespace Pwneu.IntegrationTests.Features.BlacklistedEmails;

[Collection(nameof(IntegrationTestCollection))]
public class RemoveBlacklistedEmailTests(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotRemoveBlacklistedEmail_WhenEmailDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.CreateVersion7();

        // Act
        var removeEmailResult = await Sender.Send(
            new RemoveBlacklistedEmail.Command(nonExistentId)
        );

        // Assert
        removeEmailResult.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_RemoveBlacklistedEmail_WhenEmailExists()
    {
        // Arrange
        var blacklistedEmail = BlacklistedEmail.Create(F.Internet.Email());
        await DbContext.BlacklistedEmails.AddAsync(blacklistedEmail);
        await DbContext.SaveChangesAsync();

        // Act
        var removeEmailResult = await Sender.Send(
            new RemoveBlacklistedEmail.Command(blacklistedEmail.Id)
        );
        var deletedEmail = DbContext.BlacklistedEmails.FirstOrDefault(b =>
            b.Id == blacklistedEmail.Id
        );

        // Assert
        removeEmailResult.IsSuccess.Should().BeTrue();
        deletedEmail.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_InvalidateCache_WhenBlacklistedEmailWasRemoved()
    {
        // Arrange
        var blacklistedEmail = BlacklistedEmail.Create(F.Internet.Email());
        await DbContext.BlacklistedEmails.AddAsync(blacklistedEmail);
        await DbContext.SaveChangesAsync();

        await Cache.SetAsync(CacheKeys.BlacklistedEmails(), new List<BlacklistedEmail>());

        // Act
        await Sender.Send(new RemoveBlacklistedEmail.Command(blacklistedEmail.Id));

        // Assert
        var blacklistedEmailsCache = await Cache.GetOrDefaultAsync<List<BlacklistedEmail>>(
            CacheKeys.BlacklistedEmails()
        );
        blacklistedEmailsCache.Should().BeNull();
    }
}
