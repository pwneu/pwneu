using System.Text;
using FluentAssertions;
using Pwneu.Api.Features.Artifacts;
using Pwneu.Api.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Api.IntegrationTests.Features.Artifacts;

[Collection(nameof(IntegrationTestCollection))]
public class GetArtifactTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetArtifact_WhenArtifactExists()
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

        var artifactData = new ArtifactDataResponse(
            FileName: artifact.FileName,
            ContentType: artifact.ContentType,
            Data: artifact.Data);

        // Act
        var getArtifact = await Sender.Send(new GetArtifact.Query(artifact.Id));

        // Assert
        getArtifact.Should().NotBeNull();
        getArtifact.Should().BeOfType<Result<ArtifactDataResponse>>();
        getArtifact.Value.Should().BeEquivalentTo(artifactData);
    }

    [Fact]
    public async Task Handle_Should_NotGetArtifact_WhenArtifactDoesNotExists()
    {
        // Act
        var getArtifact = new GetArtifact.Query(Guid.NewGuid());
        var artifact = await Sender.Send(getArtifact);

        // Assert
        artifact.IsSuccess.Should().BeFalse();
    }
}