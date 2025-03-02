using FluentAssertions;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Features.Challenges;

namespace Pwneu.IntegrationTests.Features.Challenges;

[Collection(nameof(IntegrationTestCollection))]
public class CreateChallengeTests(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotCreateChallenge_WhenCommandIsNotValid()
    {
        // Arrange
        var category = await AddValidCategoryToDatabaseAsync();
        var categoryId = category.Id;

        var createChallenges = new List<CreateChallenge.Command>
        {
            new(
                categoryId,
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
                categoryId,
                "Sanity Check",
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
                categoryId,
                "Sanity Check",
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
        var createChallengeResults = await Task.WhenAll(
            createChallenges.Select(invalidChallenge => Sender.Send(invalidChallenge)).ToList()
        );

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
        var category = await AddValidCategoryToDatabaseAsync();
        var categoryId = category.Id;

        var createChallenge = new CreateChallenge.Command(
            categoryId,
            "Sanity Check",
            "The flag is in plain sight",
            50,
            true,
            DateTime.Now.AddDays(7),
            5,
            [],
            ["flag1", "flag2"],
            string.Empty,
            string.Empty
        );

        // Act
        var createChallengeResult = await Sender.Send(createChallenge);
        var challenge = DbContext.Challenges.FirstOrDefault(c =>
            c.Id == createChallengeResult.Value
        );

        // Assert
        createChallengeResult.Should().BeOfType<Result<Guid>>();
        createChallengeResult.IsSuccess.Should().BeTrue();
        challenge.Should().NotBeNull();
        challenge!.Id.Should().Be(createChallengeResult.Value);
    }

    [Fact]
    public async Task Handle_Should_CreateChallenge_WhenCommandIsValid()
    {
        // Arrange
        var category = await AddValidCategoryToDatabaseAsync();
        var categoryId = category.Id;

        var createChallenge = new CreateChallenge.Command(
            category.Id,
            CommonConstants.Unknown,
            CommonConstants.Unknown,
            50,
            true,
            DateTime.UtcNow.AddDays(7),
            5,
            [],
            [CommonConstants.Unknown, CommonConstants.Unknown],
            string.Empty,
            string.Empty
        );

        // Act
        var createChallengeResult = await Sender.Send(createChallenge);
        var challenge = DbContext.Challenges.FirstOrDefault(c =>
            c.Id == createChallengeResult.Value
        );

        // Assert
        createChallengeResult.Should().BeOfType<Result<Guid>>();
        createChallengeResult.IsSuccess.Should().BeTrue();
        challenge.Should().NotBeNull();
        challenge!.Id.Should().Be(createChallengeResult.Value);
    }
}
