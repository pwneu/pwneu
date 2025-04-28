using FluentAssertions;
using Pwneu.Api.Features.Challenges;

namespace Pwneu.IntegrationTests.Features.Challenges;

[Collection(nameof(IntegrationTestCollection))]
public class DeleteChallengeTests(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotDeleteChallenge_WhenChallengeDoesNotExists()
    {
        // Act
        var deleteChallenge = await Sender.Send(
            new DeleteChallenge.Command(Guid.CreateVersion7(), string.Empty, string.Empty)
        );

        // Assert
        deleteChallenge.IsSuccess.Should().BeFalse();
    }
}
