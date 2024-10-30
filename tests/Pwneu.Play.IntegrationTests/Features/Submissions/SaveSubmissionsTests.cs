using FluentAssertions;
using Pwneu.Play.Features.Submissions;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.IntegrationTests.Features.Submissions;

[Collection(nameof(IntegrationTestCollection))]
public class SaveSubmissionsTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_SaveSubmissions()
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

        var submittedEvents = new List<SubmittedEvent>
        {
            new()
            {
                UserId = "true",
                UserName = F.Lorem.Word(),
                ChallengeId = challengeId,
                Flag = "flag",
                SubmittedAt = DateTime.UtcNow,
            }
        };

        // Act
        var submit = await Sender.Send(new SaveSubmissions.Command(submittedEvents));

        // Assert
        submit.IsSuccess.Should().BeTrue();
    }
}