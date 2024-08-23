using FluentAssertions;
using Pwneu.Identity.Features.Profile;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Identity.IntegrationTests.Features.Profile;

[Collection(nameof(IntegrationTestCollection))]
public class ChangeFullNameTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotChangeFullName_WhenCommandIsNotValid()
    {
        // Arrange
        var userId = TestUser.Id;
        var invalidCommands = new List<ChangeFullName.Command>
        {
            new(userId, string.Empty), // Invalid full name
            new(string.Empty, F.Lorem.Word()) // Invalid user ID
        };

        // Act
        var results = await Task.WhenAll(invalidCommands.Select(cmd => Sender.Send(cmd)));

        // Assert
        foreach (var result in results)
        {
            result.IsSuccess.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Handle_Should_NotChangeFullName_WhenUserDoesNotExist()
    {
        // Act
        var result = await Sender.Send(new ChangeFullName.Command(
            UserId: Guid.NewGuid().ToString(),
            NewFullName: F.Lorem.Word()));

        // Assert
        result.Should().BeOfType<Result>();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_ChangeFullName_WhenCommandIsValid()
    {
        // Arrange
        var userId = TestUser.Id;
        var newFullName = F.Person.FullName;

        // Act
        var result = await Sender.Send(new ChangeFullName.Command(userId, newFullName));
        var user = await DbContext.Users.FindAsync(userId);

        // Assert
        result.Should().BeOfType<Result>();
        result.IsSuccess.Should().BeTrue();
        user.Should().NotBeNull();
        user.FullName.Should().Be(newFullName);
    }

    [Fact]
    public async Task Handle_Should_InvalidateUserCache()
    {
        // Arrange
        var userId = TestUser.Id;
        var newFullName = F.Person.FullName;

        await Cache.SetAsync(Keys.User(userId), new UserResponse());
        await Cache.SetAsync(Keys.UserDetails(userId), new UserDetailsResponse());

        // Act
        await Sender.Send(new ChangeFullName.Command(userId, newFullName));

        var userCache = Cache.GetOrDefault<UserResponse>(Keys.User(userId));
        var userDetailsCache = Cache.GetOrDefault<UserDetailsResponse>(Keys.UserDetails(userId));

        // Assert
        userCache.Should().BeNull();
        userDetailsCache.Should().BeNull();
    }
}