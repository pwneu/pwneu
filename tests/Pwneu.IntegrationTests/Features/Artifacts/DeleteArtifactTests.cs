using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Entities;
using Pwneu.Api.Features.Artifacts;
using System.Text;

namespace Pwneu.IntegrationTests.Features.Artifacts;

[Collection(nameof(IntegrationTestCollection))]
public class DeleteArtifactTests(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_DeleteArtifact_WhenArtifactExists()
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

        // Act
        var deleteArtifact = await Sender.Send(
            new DeleteArtifact.Command(artifact.Id, string.Empty, string.Empty)
        );
        var deletedArtifact = await DbContext
            .Artifacts.Where(a => a.Id == artifact.Id)
            .FirstOrDefaultAsync();

        // Assert
        deletedArtifact.Should().BeNull();
        deleteArtifact.Should().NotBeNull();
        deleteArtifact.Should().BeOfType<Result>();
    }

    [Fact]
    public async Task Handle_Should_NotDeleteArtifact_WhenArtifactDoesNotExists()
    {
        // Act
        var deleteArtifact = await Sender.Send(
            new DeleteArtifact.Command(Guid.NewGuid(), string.Empty, string.Empty)
        );

        // Assert
        deleteArtifact.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_InvalidateCache()
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

        await Cache.SetAsync(
            CacheKeys.ChallengeDetails(challengeId),
            new ChallengeDetailsResponse()
        );
        await Cache.SetAsync(CacheKeys.ArtifactData(challengeId), new ArtifactDataResponse());

        // Act
        await Sender.Send(new DeleteArtifact.Command(artifact.Id, string.Empty, string.Empty));

        var cachedChallenge = await Cache.GetOrDefaultAsync<ChallengeDetailsResponse>(
            CacheKeys.ChallengeDetails(challenge.Id)
        );
        var cachedArtifact = await Cache.GetOrDefaultAsync<ArtifactDataResponse>(
            CacheKeys.ArtifactData(artifact.Id)
        );

        // Assert
        cachedArtifact.Should().BeNull();
        cachedChallenge.Should().BeNull();
    }
}
