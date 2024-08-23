using FluentAssertions;
using Pwneu.Identity.Features.Profile;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Identity.IntegrationTests.Features.Profile;

[Collection(nameof(IntegrationTestCollection))]
public class ChangeUserNameTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotChangeUserName_WhenCommandIsNotValid()
    {
        // Arrange
        var userId = TestUser.Id;
        var invalidCommands = new List<ChangeUserName.Command>
        {
            new(userId, string.Empty) // Empty new userName
        };

        // Act
        var results = await Task.WhenAll(invalidCommands.Select(cmd => Sender.Send(cmd)));

        // Assert
        foreach (var result in results)
        {
            result.IsSuccess.Should().BeFalse();
            result.Error.Code.Should().Be("ChangeUserName.Validation");
        }
    }

    [Fact]
    public async Task Handle_Should_NotChangeUserName_WhenUserDoesNotExist()
    {
        // Act
        var result = await Sender.Send(new ChangeUserName.Command(
            UserId: Guid.NewGuid().ToString(),
            NewUserName: "NewUserName"));

        // Assert
        result.Should().BeOfType<Result>();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_ChangeUserName_WhenCommandIsValid()
    {
        // Arrange
        var userId = TestUser.Id;
        const string newUserName = "NewUserName";

        // Act
        var result = await Sender.Send(new ChangeUserName.Command(
            UserId: userId,
            NewUserName: newUserName));

        var user = await UserManager.FindByIdAsync(userId);

        // Assert
        result.Should().BeOfType<Result>();
        result.IsSuccess.Should().BeTrue();
        user!.UserName.Should().Be(newUserName);
    }

    [Fact]
    public async Task Handle_Should_InvalidateUserCache_WhenUserNameIsChanged()
    {
        // Arrange
        var userId = TestUser.Id;
        const string newUserName = "NewUserName";

        await Cache.SetAsync(Keys.User(userId), new UserResponse());
        await Cache.SetAsync(Keys.UserDetails(userId), new UserDetailsResponse());

        // Act
        await Sender.Send(new ChangeUserName.Command(
            UserId: userId,
            NewUserName: newUserName));

        var userCache = Cache.GetOrDefault<UserResponse>(Keys.User(userId));
        var userDetailsCache = Cache.GetOrDefault<UserDetailsResponse>(Keys.UserDetails(userId));

        // Assert
        userCache.Should().BeNull();
        userDetailsCache.Should().BeNull();
    }
}