using FluentAssertions;
using Pwneu.Api.Common;
using Pwneu.Api.Contracts;
using Pwneu.Api.Entities;
using Pwneu.Api.Features.Artifacts;
using System.Text;

namespace Pwneu.IntegrationTests.Features.Artifacts;

[Collection(nameof(IntegrationTestCollection))]
public class GetArtifactTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetArtifact_WhenArtifactExists()
    {
        // Arrange
        var category = await AddValidCategoryToDatabaseAsync();
        var challenge = await AddValidChallengeToDatabaseAsync(category.Id);
        var challengeId = challenge.Id;

        var artifact = Artifact.Create(
            challengeId,
            F.System.FileName(),
            F.System.MimeType(),
            Encoding.UTF8.GetBytes(F.Lorem.Text())
        );

        DbContext.Add(artifact);
        await DbContext.SaveChangesAsync();

        var artifactData = new ArtifactDataResponse
        {
            FileName = artifact.FileName,
            ContentType = artifact.ContentType,
            Data = artifact.Data
        };

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
