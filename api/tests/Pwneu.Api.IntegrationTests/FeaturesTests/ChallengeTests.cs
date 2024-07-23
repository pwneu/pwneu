using FluentAssertions;
using Pwneu.Api.Features.Challenges;
using Pwneu.Api.Shared.Common;

namespace Pwneu.Api.IntegrationTests.FeaturesTests;

public class ChallengeTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Create_ShouldAddChallenge()
    {
        // Arrange
        var command = new CreateChallenge.Command("Sanity Check", "The flag is in plain sight", 50, true,
            DateTime.UtcNow.AddDays(7), 5, ["flag1", "flag2"]);

        // Act
        var result = await Sender.Send(command);
        var challenge = DbContext.Challenges.FirstOrDefault(c => c.Id == result.Value);

        // Assert
        result.Should().BeOfType<Result<Guid>>();
        result.IsFailure.Should().BeFalse();
        challenge.Should().NotBeNull();
        challenge.Id.Should().Be(result.Value);
    }
}