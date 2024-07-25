using FluentAssertions;
using Pwneu.Api.Features.Flags;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.IntegrationTests.Features.Flags;

[Collection(nameof(IntegrationTestCollection))]
public class GetChallengeFlagsTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetChallengeFlags_WhenChallengeExists()
    {
        // Arrange
        var challenge = new Challenge
        {
            Id = Guid.NewGuid(),
            Name = F.Lorem.Word(),
            Description = F.Lorem.Sentence(),
            Points = F.Random.Int(1, 100),
            DeadlineEnabled = F.Random.Bool(),
            Deadline = DateTime.UtcNow,
            MaxAttempts = F.Random.Int(1, 10),
            Flags = F.Lorem.Words().ToList()
        };

        DbContext.Add(challenge);
        await DbContext.SaveChangesAsync();

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