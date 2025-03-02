using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Contracts;
using Pwneu.Api.Features.Challenges;

namespace Pwneu.IntegrationTests.Features.Challenges;

[Collection(nameof(IntegrationTestCollection))]
public class GetChallengesTests(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetChallenges()
    {
        // Arrange
        await DbContext.Challenges.ExecuteDeleteAsync();
        var category = await AddValidCategoryToDatabaseAsync();

        var challengeIds = new List<Guid>();
        foreach (var _ in Enumerable.Range(1, 3))
        {
            var challenge = await AddValidChallengeToDatabaseAsync(category.Id);
            challengeIds.Add(challenge.Id);
        }

        // Act
        var getChallenges = new GetChallenges.Query();
        var getChallengesResult = await Sender.Send(getChallenges);

        // Assert
        getChallengesResult.IsSuccess.Should().BeTrue();
        getChallengesResult.Should().BeOfType<Result<PagedList<ChallengeResponse>>>();
    }
}
