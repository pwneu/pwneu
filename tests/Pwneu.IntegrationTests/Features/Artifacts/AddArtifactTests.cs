using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Pwneu.Api.Common;
using Pwneu.Api.Features.Artifacts;

namespace Pwneu.IntegrationTests.Features.Artifacts;

[Collection(nameof(IntegrationTestCollection))]
public class AddArtifactTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_AddArtifact_WhenChallengeExists()
    {
        // Arrange
        var category = await AddValidCategoryToDatabaseAsync();
        var challenge = await AddValidChallengeToDatabaseAsync(category.Id);
        var challengeId = challenge.Id;

        var fileContent = "PWNEU{@1Ways_T3$t_yoUr_$OFt@War3}"u8.ToArray();
        var formStream = new MemoryStream(fileContent);
        var file = new FormFile(formStream, 0, fileContent.Length, F.Lorem.Word(), F.System.FileName())
        {
            Headers = new HeaderDictionary(),
            ContentType = F.System.MimeType()
        };

        // Act
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);

        var addArtifact = await Sender.Send(new AddArtifact.Command(
            ChallengeId: challengeId,
            FileName: file.FileName,
            FileSize: file.Length,
            ContentType: file.ContentType,
            Data: stream.ToArray(),
            UserName: string.Empty,
            UserId: string.Empty));

        var artifact = DbContext.Artifacts.FirstOrDefault(a => a.Id == addArtifact.Value);

        // Assert
        addArtifact.Should().BeOfType<Result<Guid>>();
        addArtifact.IsSuccess.Should().BeTrue();
        artifact.Should().NotBeNull();
    }
}
