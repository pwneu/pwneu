using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Pwneu.Api.Features.ChallengeFiles;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.IntegrationTests.Features.ChallengeFiles;

[Collection(nameof(IntegrationTestCollection))]
public class AddChallengeFileTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_AddChallengeFile_WhenChallengeExists()
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

        var addChallengeFile = await Sender.Send(new AddChallengeFile.Command(
            ChallengeId: challengeId,
            FileName: file.FileName,
            ContentType: file.ContentType,
            Data: stream.ToArray()));

        var challengeFile = DbContext.ChallengeFiles.FirstOrDefault(cf => cf.Id == addChallengeFile.Value);

        // Assert
        addChallengeFile.Should().BeOfType<Result<Guid>>();
        addChallengeFile.IsSuccess.Should().BeTrue();
        challengeFile.Should().NotBeNull();
    }
}