using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Features.Artifacts;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.IntegrationTests.Features.Artifacts;

[Collection(nameof(IntegrationTestCollection))]
public class DeleteArtifactTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_DeleteArtifact_WhenArtifactExists()
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

        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            ChallengeId = challenge.Id,
            FileName = F.System.FileName(),
            ContentType = F.System.MimeType(),
            Data = Encoding.UTF8.GetBytes(F.Lorem.Text()),
            Challenge = challenge
        };
        DbContext.Add(artifact);

        await DbContext.SaveChangesAsync();

        // Act
        var deleteArtifact = await Sender.Send(new DeleteArtifact.Command(artifact.Id, string.Empty, string.Empty));
        var deletedArtifact = await DbContext.Artifacts.Where(a => a.Id == artifact.Id).FirstOrDefaultAsync();

        // Assert
        deletedArtifact.Should().BeNull();
        deleteArtifact.Should().NotBeNull();
        deleteArtifact.Should().BeOfType<Result>();
    }

    [Fact]
    public async Task Handle_Should_NotDeleteArtifact_WhenArtifactDoesNotExists()
    {
        // Act
        var deleteArtifact = await Sender.Send(new DeleteArtifact.Command(Guid.NewGuid(), string.Empty, string.Empty));

        // Assert
        deleteArtifact.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_InvalidateChallengeAndArtifactCache()
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

        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            ChallengeId = challenge.Id,
            FileName = F.System.FileName(),
            ContentType = F.System.MimeType(),
            Data = Encoding.UTF8.GetBytes(F.Lorem.Text()),
            Challenge = challenge
        };
        DbContext.Add(artifact);

        await DbContext.SaveChangesAsync();

        // Act
        await Cache.SetAsync(Keys.ChallengeDetails(challenge.Id), challenge);
        await Cache.SetAsync(Keys.ArtifactData(challenge.Id), artifact);

        await Sender.Send(new DeleteArtifact.Command(artifact.Id, string.Empty, string.Empty));

        var cachedChallenge = await Cache.GetOrDefaultAsync<ChallengeDetailsResponse>(
            Keys.ChallengeDetails(challenge.Id));
        var cachedArtifact = await Cache.GetOrDefaultAsync<ArtifactDataResponse>(
            Keys.ArtifactData(artifact.Id));

        // Assert
        cachedArtifact.Should().BeNull();
        cachedChallenge.Should().BeNull();
    }
}