using FluentAssertions;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
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

    [Fact]
    public async Task Handle_Should_DeleteChallenge_WhenChallengeExists()
    {
        // Arrange
        var category = await AddValidCategoryToDatabaseAsync();
        var challenge = await AddValidChallengeToDatabaseAsync(category.Id);
        var challengeId = challenge.Id;

        // Act
        var deleteChallenge = await Sender.Send(
            new DeleteChallenge.Command(challengeId, string.Empty, string.Empty)
        );
        var deletedChallenge = DbContext.Challenges.FirstOrDefault(c => c.Id == challengeId);

        // Assert
        deleteChallenge.IsSuccess.Should().BeTrue();
        deletedChallenge.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_InvalidateChallengeCache_WhenChallengeWasDeleted()
    {
        // Arrange
        var category = await AddValidCategoryToDatabaseAsync();
        var challenge = await AddValidChallengeToDatabaseAsync(category.Id);
        var challengeId = challenge.Id;

        await Cache.SetAsync(CacheKeys.ChallengeDetails(challengeId), challenge);

        // Act
        await Sender.Send(new DeleteChallenge.Command(challengeId, string.Empty, string.Empty));
        var challengeCache = Cache.GetOrDefault<ChallengeResponse>(
            CacheKeys.ChallengeDetails(challengeId)
        );

        // Assert
        challengeCache.Should().BeNull();
    }
}
