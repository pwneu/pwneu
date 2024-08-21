using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Features.Submissions;
using Pwneu.Play.Shared.Entities;

namespace Pwneu.Play.IntegrationTests.Features.Submissions;

[Collection(nameof(IntegrationTestCollection))]
public class SaveSubmissionTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_IncreaseChallengeCount_WhenFlagIsCorrect()
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
            DeadlineEnabled = false,
            Deadline = DateTime.UtcNow,
            MaxAttempts = F.Random.Int(1, 10),
            Flags = ["flag"]
        });
        await DbContext.SaveChangesAsync();

        // Act
        await Sender.Send(new SaveSubmission.Command(
            UserId: "true",
            ChallengeId: challengeId,
            Flag: "flag",
            SubmittedAt: DateTime.UtcNow,
            IsCorrect: true));
        var updatedChallenge = await DbContext.Challenges.Where(ch => ch.Id == challengeId).FirstOrDefaultAsync();

        // Assert
        updatedChallenge.Should().NotBeNull();
        updatedChallenge.SolveCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_NotSaveSubmission_WhenAlreadySolved()
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
            DeadlineEnabled = false,
            Deadline = DateTime.UtcNow,
            MaxAttempts = F.Random.Int(1, 10),
            Flags = ["flag"]
        });
        await DbContext.SaveChangesAsync();

        // Act
        await Sender.Send(new SaveSubmission.Command(
            UserId: "true",
            ChallengeId: challengeId,
            Flag: "flag",
            SubmittedAt: DateTime.UtcNow,
            IsCorrect: true));
        var secondSubmit = await Sender.Send(new SaveSubmission.Command(
            UserId: "true",
            ChallengeId: challengeId,
            Flag: "flag",
            SubmittedAt: DateTime.UtcNow,
            IsCorrect: true));

        // Assert
        secondSubmit.IsSuccess.Should().BeFalse();
    }
}