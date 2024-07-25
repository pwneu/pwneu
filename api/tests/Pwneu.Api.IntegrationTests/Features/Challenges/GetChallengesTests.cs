using FluentAssertions;
using Pwneu.Api.Features.Challenges;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.IntegrationTests.Features.Challenges;

[Collection(nameof(IntegrationTestCollection))]
public class GetChallengesTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetChallenges()
    {
        // Arrange
        foreach (var unused in Enumerable.Range(1, 3))
        {
            var id = Guid.NewGuid();
            DbContext.Add(new Challenge
            {
                Id = id,
                Name = F.Lorem.Word(),
                Description = F.Lorem.Sentence(),
                Points = F.Random.Int(1, 100),
                DeadlineEnabled = F.Random.Bool(),
                Deadline = DateTime.UtcNow,
                MaxAttempts = F.Random.Int(1, 10),
                Flags = F.Lorem.Words().ToList()
            });
            await DbContext.SaveChangesAsync();
        }

        // Act
        var getChallenges = new GetChallenges.Query();
        var getChallengesResult = await Sender.Send(getChallenges);

        // Assert
        getChallengesResult.IsSuccess.Should().BeTrue();
        getChallengesResult.Should().BeOfType<Result<PagedList<ChallengeDetailsResponse>>>();
    }
}