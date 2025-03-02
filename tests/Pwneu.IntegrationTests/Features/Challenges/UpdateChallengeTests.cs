using Bogus;
using FluentAssertions;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Extensions.Entities;
using Pwneu.Api.Features.Challenges;

namespace Pwneu.IntegrationTests.Features.Challenges;

[Collection(nameof(IntegrationTestCollection))]
public class UpdateChallengeTests(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotUpdateChallenge_WhenCommandIsNotValid()
    {
        // Arrange
        var category = await AddValidCategoryToDatabaseAsync();

        var challengeIds = new List<Guid>();
        foreach (var _ in Enumerable.Range(1, 3))
        {
            var challenge = await AddValidChallengeToDatabaseAsync(category.Id);
            challengeIds.Add(challenge.Id);
        }

        var updatedChallenges = new List<UpdateChallenge.Command>
        {
            new(
                challengeIds[0],
                string.Empty,
                F.Lorem.Sentence(),
                50,
                false,
                DateTime.UtcNow,
                5,
                [],
                F.Lorem.Words(),
                string.Empty,
                string.Empty
            ),
            new(
                challengeIds[1],
                F.Lorem.Word(),
                string.Empty,
                50,
                false,
                DateTime.UtcNow,
                5,
                [],
                F.Lorem.Words(),
                string.Empty,
                string.Empty
            ),
            new(
                challengeIds[2],
                F.Lorem.Word(),
                F.Lorem.Sentence(),
                50,
                false,
                DateTime.UtcNow,
                5,
                [],
                [],
                string.Empty,
                string.Empty
            ),
        };

        // Act
        var updateChallenges = new List<Result>();
        foreach (var updatedChallenge in updatedChallenges)
        {
            var updateChallenge = await Sender.Send(updatedChallenge);
            updateChallenges.Add(updateChallenge);
        }

        // Assert
        foreach (var updateChallenge in updateChallenges)
            updateChallenge.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_NotUpdateChallenge_WhenChallengeDoesNotExists()
    {
        // Act
        var updateChallenge = await Sender.Send(
            new UpdateChallenge.Command(
                Id: Guid.NewGuid(),
                Name: F.Lorem.Word(),
                Description: F.Lorem.Sentence(),
                Points: 50,
                DeadlineEnabled: false,
                Deadline: DateTime.UtcNow,
                MaxAttempts: 5,
                Tags: [],
                Flags: F.Lorem.Words(),
                string.Empty,
                string.Empty
            )
        );

        // Assert
        updateChallenge.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_GetDifferentChallengeDetails()
    {
        // Arrange
        var category = await AddValidCategoryToDatabaseAsync();
        var challenge = await AddValidChallengeToDatabaseAsync(category.Id);

        // Act
        var faker = new Faker();
        var updateChallenge = await Sender.Send(
            new UpdateChallenge.Command(
                Id: challenge.Id,
                Name: faker.Lorem.Word(),
                Description: faker.Lorem.Sentence(),
                Points: faker.Random.Int(101, 200),
                DeadlineEnabled: true,
                Deadline: DateTime.Now,
                MaxAttempts: faker.Random.Int(11, 20),
                Tags: [],
                Flags: faker.Lorem.Words(),
                string.Empty,
                string.Empty
            )
        );

        var updatedChallenge = await DbContext.GetChallengeDetailsByIdAsync(challenge.Id);

        // Assert
        updateChallenge.IsSuccess.Should().BeTrue();
        updatedChallenge.Should().NotBeNull();
        updatedChallenge.Should().NotBeEquivalentTo(challenge);
    }

    [Fact]
    public async Task Handle_Should_InvalidateChallengeCache()
    {
        // Arrange
        var category = await AddValidCategoryToDatabaseAsync();
        var challenge = await AddValidChallengeToDatabaseAsync(category.Id);
        await Cache.SetAsync(CacheKeys.ChallengeDetails(challenge.Id), challenge);

        // Act
        await Sender.Send(
            new UpdateChallenge.Command(
                Id: challenge.Id,
                Name: F.Lorem.Word(),
                Description: F.Lorem.Sentence(),
                Points: 50,
                DeadlineEnabled: false,
                Deadline: DateTime.UtcNow,
                MaxAttempts: 5,
                Tags: [],
                Flags: F.Lorem.Words(),
                string.Empty,
                string.Empty
            )
        );

        var challengeCache = Cache.GetOrDefault<ChallengeResponse>(
            CacheKeys.ChallengeDetails(challenge.Id)
        );

        // Assert
        challengeCache.Should().BeNull();
    }
}
