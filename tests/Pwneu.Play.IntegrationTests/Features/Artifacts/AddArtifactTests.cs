using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Pwneu.Play.Features.Artifacts;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;

namespace Pwneu.Play.IntegrationTests.Features.Artifacts;

[Collection(nameof(IntegrationTestCollection))]
public class AddArtifactTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_AddArtifact_WhenChallengeExists()
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
            DeadlineEnabled = F.Random.Bool(),
            Deadline = DateTime.UtcNow,
            MaxAttempts = F.Random.Int(1, 10),
            Flags = F.Lorem.Words().ToList()
        });
        await DbContext.SaveChangesAsync();

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
            Data: stream.ToArray()));

        var artifact = DbContext.Artifacts.FirstOrDefault(a => a.Id == addArtifact.Value);

        // Assert
        addArtifact.Should().BeOfType<Result<Guid>>();
        addArtifact.IsSuccess.Should().BeTrue();
        artifact.Should().NotBeNull();
    }
}