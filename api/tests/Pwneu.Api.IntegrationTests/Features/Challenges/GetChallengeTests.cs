using FluentAssertions;
using Pwneu.Api.Features.Challenges;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;

namespace Pwneu.Api.IntegrationTests.Features.Challenges;

[Collection(nameof(IntegrationTestCollection))]
public class GetChallengeTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetChallenge_WhenChallengeExists()
    {
        // Arrange
        var createChallenge = new CreateChallenge.Command("Sanity Check", "The flag is in plain sight", 50, true,
            DateTime.UtcNow.AddDays(7), 5, ["flag1", "flag2"]);
        var challengeId = (await Sender.Send(createChallenge)).Value;

        var challenge = new ChallengeDetailsResponse(challengeId, createChallenge.Name, createChallenge.Description,
            createChallenge.Points, createChallenge.DeadlineEnabled, createChallenge.Deadline,
            createChallenge.MaxAttempts, []);

        // Act
        var getChallenge = new GetChallenge.Query(challengeId);
        var getChallengeResult = await Sender.Send(getChallenge);

        // Assert
        getChallengeResult.IsSuccess.Should().BeTrue();
        getChallengeResult.Should().BeOfType<Result<ChallengeDetailsResponse>>();
        getChallengeResult.Value.Should().BeEquivalentTo(challenge);
    }

    [Fact]
    public async Task Handle_Should_NotGetChallenge_WhenChallengeDoesNotExists()
    {
        // Arrange
        var challengeId = Guid.NewGuid();

        // Act
        var getChallenge = new GetChallenge.Query(challengeId);
        var getChallengeResult = await Sender.Send(getChallenge);

        // Assert
        getChallengeResult.IsSuccess.Should().BeFalse();
    }
}