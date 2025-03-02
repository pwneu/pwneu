using FluentAssertions;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Features.Challenges;

namespace Pwneu.IntegrationTests.Features.Challenges;

[Collection(nameof(IntegrationTestCollection))]
public class GetChallengeTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetChallenge_WhenChallengeExists()
    {
        // Arrange
        var category = await AddValidCategoryToDatabaseAsync();
        var challenge = await AddValidChallengeToDatabaseAsync(category.Id);
        var challengeId = challenge.Id;

        // Act
        var getChallenge = new GetChallenge.Query(challengeId, CommonConstants.Unknown);
        var getChallengeResult = await Sender.Send(getChallenge);

        // Assert
        getChallengeResult.IsSuccess.Should().BeTrue();
        getChallengeResult.Should().BeOfType<Result<ChallengeDetailsNoFlagResponse>>();
    }

    [Fact]
    public async Task Handle_Should_NotGetChallenge_WhenChallengeDoesNotExists()
    {
        // Arrange
        var challengeId = Guid.NewGuid();

        // Act
        var getChallenge = new GetChallenge.Query(challengeId, CommonConstants.Unknown);
        var getChallengeResult = await Sender.Send(getChallenge);

        // Assert
        getChallengeResult.IsSuccess.Should().BeFalse();
    }
}
