using FluentAssertions;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Features.Submissions;

namespace Pwneu.IntegrationTests.Features.Submissions;

[Collection(nameof(IntegrationTestCollection))]
public class SubmitFlagTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Theory]
    [InlineData(FlagStatus.Incorrect, "flag3", "flag1", "flag2")]
    [InlineData(FlagStatus.Correct, "flag1", "flag1", "flag2")]
    public async Task Handle_Should_SubmitFlag_WithExpectedFlagStatus(
        FlagStatus expected,
        string value,
        params string[] flags
    )
    {
        // Arrange
        var category = await AddValidCategoryToDatabaseAsync();
        var challenge = Challenge.Create(
            category.Id,
            CommonConstants.Unknown,
            CommonConstants.Unknown,
            0,
            false,
            DateTime.MaxValue,
            0,
            [],
            [.. flags]
        );
        DbContext.Add(challenge);
        await DbContext.SaveChangesAsync();

        await DbContext.SetConfigurationValueAsync(ConfigurationKeys.SubmissionsAllowed, true);
        await Cache.RemoveAsync(CacheKeys.SubmissionsAllowed());

        // Act
        var submitFlag = await Sender.Send(
            new SubmitFlag.Command(TestUser.Id, CommonConstants.Unknown, challenge.Id, value)
        );

        // Assert
        submitFlag.IsSuccess.Should().BeTrue();
        submitFlag.Value.Should().Be(expected);
    }

    [Fact]
    public async Task SubmitFlag_Should_NotSubmitFlag_WhenChallengeDoesNotExists()
    {
        // Arrange
        var challengeId = Guid.NewGuid();

        // Act
        var submitFlag = await Sender.Send(
            new SubmitFlag.Command(Guid.NewGuid().ToString(), "user", challengeId, F.Lorem.Word())
        );

        // Assert
        submitFlag.IsSuccess.Should().BeFalse();
    }
}
