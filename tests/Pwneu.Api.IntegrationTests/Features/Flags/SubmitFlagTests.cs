using FluentAssertions;
using Pwneu.Api.Features.Flags;
using Pwneu.Api.Shared.Entities;
using Pwneu.Shared.Contracts;

namespace Pwneu.Api.IntegrationTests.Features.Flags;

[Collection(nameof(IntegrationTestCollection))]
public class SubmitFlagTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Theory]
    [InlineData(FlagStatus.Incorrect, "flag3", "flag1", "flag2")]
    [InlineData(FlagStatus.Correct, "flag1", "flag1", "flag2")]
    public async Task Handle_Should_SubmitFlag_WithExpectedFlagStatus(FlagStatus expected, string value,
        params string[] flags)
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var category = new Category
        {
            Id = categoryId,
            Name = F.Lorem.Word(),
            Description = F.Lorem.Sentence()
        };
        DbContext.Add(category);
        await DbContext.SaveChangesAsync();

        var challengeId = Guid.NewGuid();
        DbContext.Add(new Challenge
        {
            Id = challengeId,
            CategoryId = categoryId,
            Name = F.Lorem.Word(),
            Description = F.Lorem.Sentence(),
            Points = F.Random.Int(1, 100),
            DeadlineEnabled = false,
            Deadline = DateTime.UtcNow,
            MaxAttempts = F.Random.Int(1, 10),
            Flags = flags.ToList()
        });
        await DbContext.SaveChangesAsync();

        // Act
        var submitFlag = await Sender.Send(new SubmitFlag.Command(TestUser.Id, challengeId, value));

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
        var submitFlag = await Sender.Send(new SubmitFlag.Command(TestUser.Id, challengeId, F.Lorem.Word()));

        // Assert
        submitFlag.IsSuccess.Should().BeFalse();
    }
}