using FluentAssertions;
using Pwneu.Api.Features.Challenges;

namespace Pwneu.IntegrationTests.Features.Challenges;

[Collection(nameof(IntegrationTestCollection))]
public class GetChallengeFlagsTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetChallengeFlags_WhenChallengeExists()
    {
        // Arrange
        var category = await AddValidCategoryToDatabaseAsync();
        var challenge = await AddValidChallengeToDatabaseAsync(category.Id);

        // Act
        var getChallengeFlags = await Sender.Send(new GetChallengeFlags.Query(challenge.Id));

        // Assert
        getChallengeFlags.IsSuccess.Should().BeTrue();
        getChallengeFlags.Value.Should().BeEquivalentTo(challenge.Flags);
    }

    [Fact]
    public async Task Handle_Should_NotGetChallengeFlags_WhenChallengeDoesNotExists()
    {
        // Act
        var getChallengeFlags = await Sender.Send(new GetChallengeFlags.Query(Guid.NewGuid()));

        // Assert
        getChallengeFlags.IsSuccess.Should().BeFalse();
    }
}
