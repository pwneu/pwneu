using FluentAssertions;
using Pwneu.Play.Features.Challenges;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;

namespace Pwneu.Play.IntegrationTests.Features.Challenges;

[Collection(nameof(IntegrationTestCollection))]
public class CreateChallengeTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotCreateChallenge_WhenCommandIsNotValid()
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

        var createChallenges = new List<CreateChallenge.Command>
        {
            new(categoryId, string.Empty, F.Lorem.Sentence(), 50, false, DateTime.UtcNow, 5, [], F.Lorem.Words(),
                string.Empty, string.Empty),
            new(categoryId, "Sanity Check", string.Empty, 50, false, DateTime.UtcNow, 5, [], F.Lorem.Words(),
                string.Empty, string.Empty),
            new(categoryId, "Sanity Check", F.Lorem.Sentence(), 50, false, DateTime.UtcNow, 5, [], [], string.Empty,
                string.Empty)
        };

        // Act
        var createChallengeResults = await Task.WhenAll(createChallenges
            .Select(invalidChallenge => Sender.Send(invalidChallenge))
            .ToList());

        // Assert
        foreach (var createChallengeResult in createChallengeResults)
        {
            createChallengeResult.Should().BeOfType<Result<Guid>>();
            createChallengeResult.IsSuccess.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Handle_Should_CreateChallenge_EvenIfDateIsNotUtc()
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

        var createChallenge = new CreateChallenge.Command(categoryId, "Sanity Check", "The flag is in plain sight", 50,
            true,
            DateTime.Now.AddDays(7), 5, [], ["flag1", "flag2"], string.Empty, string.Empty);

        // Act
        var createChallengeResult = await Sender.Send(createChallenge);
        var challenge = DbContext.Challenges.FirstOrDefault(c => c.Id == createChallengeResult.Value);

        // Assert
        createChallengeResult.Should().BeOfType<Result<Guid>>();
        createChallengeResult.IsSuccess.Should().BeTrue();
        challenge.Should().NotBeNull();
        challenge.Id.Should().Be(createChallengeResult.Value);
    }

    [Fact]
    public async Task Handle_Should_CreateChallenge_WhenCommandIsValid()
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

        var createChallenge = new CreateChallenge.Command(categoryId, "Sanity Check", "The flag is in plain sight", 50,
            true,
            DateTime.UtcNow.AddDays(7), 5, [], ["flag1", "flag2"], string.Empty, string.Empty);

        // Act
        var createChallengeResult = await Sender.Send(createChallenge);
        var challenge = DbContext.Challenges.FirstOrDefault(c => c.Id == createChallengeResult.Value);

        // Assert
        createChallengeResult.Should().BeOfType<Result<Guid>>();
        createChallengeResult.IsSuccess.Should().BeTrue();
        challenge.Should().NotBeNull();
        challenge.Id.Should().Be(createChallengeResult.Value);
    }
}