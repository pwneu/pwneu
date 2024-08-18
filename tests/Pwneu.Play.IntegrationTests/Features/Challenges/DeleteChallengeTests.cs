using FluentAssertions;
using Pwneu.Play.Features.Challenges;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.IntegrationTests.Features.Challenges;

[Collection(nameof(IntegrationTestCollection))]
public class DeleteChallengeTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotDeleteChallenge_WhenChallengeDoesNotExists()
    {
        // Act
        var deleteChallenge = await Sender.Send(new DeleteChallenge.Command(Guid.NewGuid()));

        // Assert
        deleteChallenge.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_DeleteChallenge_WhenChallengeExists()
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
            DeadlineEnabled = F.Random.Bool(),
            Deadline = DateTime.UtcNow,
            MaxAttempts = F.Random.Int(1, 10),
            Flags = F.Lorem.Words().ToList()
        });
        await DbContext.SaveChangesAsync();

        // Act
        var deleteChallenge = await Sender.Send(new DeleteChallenge.Command(challengeId));
        var challenge = DbContext.Challenges.FirstOrDefault(c => c.Id == challengeId);

        // Assert
        deleteChallenge.IsSuccess.Should().BeTrue();
        challenge.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_InvalidateChallengeCache()
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
            DeadlineEnabled = F.Random.Bool(),
            Deadline = DateTime.UtcNow,
            MaxAttempts = F.Random.Int(1, 10),
            Flags = F.Lorem.Words().ToList()
        });
        await DbContext.SaveChangesAsync();

        await Cache.SetAsync(Keys.ChallengeDetails(challengeId), new Challenge());

        // Act
        await Sender.Send(new DeleteChallenge.Command(challengeId));
        var challengeCache = Cache.GetOrDefault<ChallengeResponse>(Keys.ChallengeDetails(challengeId));

        // Assert
        challengeCache.Should().BeNull();
    }
}