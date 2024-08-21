using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pwneu.Identity.Features.Profile;
using Pwneu.Identity.Shared.Options;
using Pwneu.Shared.Common;

namespace Pwneu.Identity.IntegrationTests.Features.Profile;

[Collection(nameof(IntegrationTestCollection))]
public class ChangePasswordTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotChangePassword_WhenCommandIsNotValid()
    {
        // Arrange
        var userId = TestUser.Id;
        var invalidCommands = new List<ChangePassword.Command>
        {
            new(userId, string.Empty, "NewPassword123!", "NewPassword123!"), // Empty current password
            new(userId, TestUserPassword, string.Empty, "NewPassword123!"), // Empty new password
            new(userId, TestUserPassword, "NewPassword123!", string.Empty), // Empty repeat password
            new(userId, TestUserPassword, "NewPassword123!", "DifferentNewPassword123!") // Mismatched passwords
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
    public async Task Handle_Should_NotChangePassword_WhenUserDoesNotExist()
    {
        // Act
        var result = await Sender.Send(new ChangePassword.Command(
            UserId: Guid.NewGuid().ToString(),
            CurrentPassword: TestUserPassword,
            NewPassword: "NewPassword123!",
            RepeatPassword: "NewPassword123!"));

        // Assert
        result.Should().BeOfType<Result>();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_NotChangePassword_WhenCurrentPasswordIsIncorrect()
    {
        // Arrange
        var userId = TestUser.Id;

        // Act
        var result = await Sender.Send(new ChangePassword.Command(
            UserId: userId,
            CurrentPassword: "WrongCurrentPassword!",
            NewPassword: "NewPassword123!",
            RepeatPassword: "NewPassword123!"));

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_ChangePassword_WhenCommandIsValid()
    {
        // Arrange
        var userId = TestUser.Id;
        const string newPassword = "NewPassword123!";

        var appOptions = Scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>().Value;

        // Act
        var result = await Sender.Send(new ChangePassword.Command(
            UserId: userId,
            CurrentPassword: appOptions.InitialAdminPassword,
            NewPassword: newPassword,
            RepeatPassword: newPassword));

        var user = await UserManager.FindByIdAsync(userId);
        var isPasswordCorrect = await UserManager.CheckPasswordAsync(user!, newPassword);

        // Assert
        result.Should().BeOfType<Result>();
        result.IsSuccess.Should().BeTrue();
        isPasswordCorrect.Should().BeTrue();
    }
}