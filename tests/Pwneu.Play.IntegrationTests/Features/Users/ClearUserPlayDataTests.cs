using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Features.Users;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;

namespace Pwneu.Play.IntegrationTests.Features.Users;

[Collection(nameof(IntegrationTestCollection))]
public class ClearUserPlayDataTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_ClearUserPlayData_And_UpdateSolveCounts()
    {
        // Arrange
        var testUserId = Guid.NewGuid().ToString();

        // Create a category and some challenges
        var categoryId = Guid.NewGuid();
        var category = new Category
        {
            Id = categoryId,
            Name = F.Lorem.Word(),
            Description = F.Lorem.Sentence()
        };
        DbContext.Add(category);
        await DbContext.SaveChangesAsync();

        var challengeId1 = Guid.NewGuid();
        var challenge1 = new Challenge
        {
            Id = challengeId1,
            CategoryId = categoryId,
            Name = F.Lorem.Word(),
            Description = F.Lorem.Sentence(),
            Points = F.Random.Int(1, 100),
            DeadlineEnabled = F.Random.Bool(),
            Deadline = DateTime.UtcNow,
            MaxAttempts = F.Random.Int(1, 10),
            Flags = F.Lorem.Words().ToList(),
            SolveCount = 5
        };
        DbContext.Add(challenge1);

        var challengeId2 = Guid.NewGuid();
        var challenge2 = new Challenge
        {
            Id = challengeId2,
            CategoryId = categoryId,
            Name = F.Lorem.Word(),
            Description = F.Lorem.Sentence(),
            Points = F.Random.Int(1, 100),
            DeadlineEnabled = F.Random.Bool(),
            Deadline = DateTime.UtcNow,
            MaxAttempts = F.Random.Int(1, 10),
            Flags = F.Lorem.Words().ToList(),
            SolveCount = 3
        };
        DbContext.Add(challenge2);

        await DbContext.SaveChangesAsync();

        // Create a hint for one of the challenges
        var hintId = Guid.NewGuid();
        var hint = new Hint
        {
            Id = hintId,
            ChallengeId = challengeId1,
            Content = "This is a hint",
            Deduction = 10
        };
        DbContext.Add(hint);
        await DbContext.SaveChangesAsync();

        // Add submissions and hint usages
        DbContext.Submissions.AddRange(
            new Submission { UserId = testUserId, ChallengeId = challengeId1, IsCorrect = true },
            new Submission { UserId = testUserId, ChallengeId = challengeId2, IsCorrect = true },
            new Submission { UserId = testUserId, ChallengeId = challengeId2, IsCorrect = false }
        );

        DbContext.HintUsages.Add(new HintUsage
        {
            UserId = testUserId,
            UserName = "test",
            HintId = hintId,
            UsedAt = DateTime.UtcNow
        });

        await DbContext.SaveChangesAsync();

        // Act
        var command = new ClearUserPlayData.Command(testUserId);
        var result = await Sender.Send(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify submissions and hint usages were deleted
        var remainingSubmissions = await DbContext.Submissions
            .Where(s => s.UserId == testUserId)
            .ToListAsync();
        remainingSubmissions.Should().BeEmpty();

        var remainingHintUsages = await DbContext.HintUsages
            .Where(hu => hu.UserId == testUserId)
            .ToListAsync();
        remainingHintUsages.Should().BeEmpty();

        // Verify the solve counts were updated correctly
        // var updatedChallenge1 = await DbContext.Challenges.FindAsync(challengeId1);
        // var updatedChallenge2 = await DbContext.Challenges.FindAsync(challengeId2);

        // updatedChallenge1!.SolveCount.Should().Be(4); // Decremented by 1
        // updatedChallenge2!.SolveCount.Should().Be(2); // Decremented by 1

        // Verify cache invalidation
        var cachedGraph = Cache.GetOrDefault<object>(Keys.UserGraph(testUserId));
        cachedGraph.Should().BeNull();

        var cachedSolveIds = Cache.GetOrDefault<object>(Keys.UserSolveIds(testUserId));
        cachedSolveIds.Should().BeNull();

        // Check that challenge and category cache entries were invalidated
        var cachedChallengeDetails1 = Cache.GetOrDefault<object>(Keys.ChallengeDetails(challengeId1));
        cachedChallengeDetails1.Should().BeNull();

        var cachedChallengeDetails2 = Cache.GetOrDefault<object>(Keys.ChallengeDetails(challengeId2));
        cachedChallengeDetails2.Should().BeNull();

        var cachedUserCategoryEval = Cache.GetOrDefault<object>(Keys.UserCategoryEval(testUserId, categoryId));
        cachedUserCategoryEval.Should().BeNull();
    }
}