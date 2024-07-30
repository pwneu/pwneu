using Bogus;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Features.Challenges;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.IntegrationTests.Features.Challenges;

[Collection(nameof(IntegrationTestCollection))]
public class UpdateChallengeTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotUpdateChallenge_WhenCommandIsNotValid()
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

        var challengeIds = new List<Guid>();
        foreach (var unused in Enumerable.Range(1, 3))
        {
            var id = Guid.NewGuid();
            challengeIds.Add(id);
            DbContext.Add(new Challenge
            {
                Id = id,
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
        }

        var updatedChallenges = new List<UpdateChallenge.Command>
        {
            new(challengeIds[0], string.Empty, F.Lorem.Sentence(), 50, false, DateTime.UtcNow, 5, F.Lorem.Words()),
            new(challengeIds[1], F.Lorem.Word(), string.Empty, 50, false, DateTime.UtcNow, 5, F.Lorem.Words()),
            new(challengeIds[2], F.Lorem.Word(), F.Lorem.Sentence(), 50, false, DateTime.UtcNow, 5, []),
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
        var updateChallenge = await Sender.Send(new UpdateChallenge.Command(
            Id: Guid.NewGuid(),
            Name: F.Lorem.Word(),
            Description: F.Lorem.Sentence(),
            Points: 50,
            DeadlineEnabled: false,
            Deadline: DateTime.UtcNow,
            MaxAttempts: 5,
            Flags: F.Lorem.Words()));

        // Assert
        updateChallenge.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_GetDifferentChallengeDetails()
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

        var challenge = new ChallengeDetailsResponse(
            Id: challengeId,
            Name: F.Lorem.Word(),
            Description: F.Lorem.Sentence(),
            Points: F.Random.Int(1, 100),
            DeadlineEnabled: F.Random.Bool(),
            Deadline: DateTime.UtcNow,
            MaxAttempts: F.Random.Int(1, 10),
            SolveCount: 0,
            ChallengeFiles: []);

        // Act
        var faker = new Faker();
        var updateChallenge = await Sender.Send(new UpdateChallenge.Command(
            Id: challenge.Id,
            Name: faker.Lorem.Word(),
            Description: faker.Lorem.Sentence(),
            Points: faker.Random.Int(101, 200),
            DeadlineEnabled: true,
            Deadline: DateTime.UtcNow,
            MaxAttempts: faker.Random.Int(11, 20),
            Flags: faker.Lorem.Words()));

        var updatedChallenge = await DbContext
            .Challenges
            .Where(c => c.Id == challenge.Id)
            .Include(c => c.ChallengeFiles)
            .Select(c => new ChallengeDetailsResponse(c.Id, c.Name, c.Description, c.Points, c.DeadlineEnabled,
                c.Deadline, c.MaxAttempts, 0, c.ChallengeFiles
                    .Select(cf => new ChallengeFileResponse(cf.Id, cf.FileName))
                    .ToList()))
            .FirstOrDefaultAsync();

        // Assert
        updateChallenge.IsSuccess.Should().BeTrue();
        updatedChallenge.Should().NotBeNull();
        updatedChallenge.Should().NotBeEquivalentTo(challenge);
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

        var challenge = new Challenge
        {
            Id = Guid.NewGuid(),
            CategoryId = categoryId,
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
        await Sender.Send(new UpdateChallenge.Command(
            Id: challenge.Id,
            Name: F.Lorem.Word(),
            Description: F.Lorem.Sentence(),
            Points: 50,
            DeadlineEnabled: false,
            Deadline: DateTime.UtcNow,
            MaxAttempts: 5,
            Flags: F.Lorem.Words()));

        var challengeCache = Cache.GetOrDefault<Challenge>($"{nameof(Challenge)}:{challenge.Id}");

        // Assert
        challengeCache.Should().BeNull();
    }
}