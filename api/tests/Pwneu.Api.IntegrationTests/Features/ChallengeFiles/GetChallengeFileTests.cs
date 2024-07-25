using System.Text;
using FluentAssertions;
using Pwneu.Api.Features.ChallengeFiles;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.IntegrationTests.Features.ChallengeFiles;

[Collection(nameof(IntegrationTestCollection))]
public class GetChallengeFileTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetChallengeFile_WhenChallengeFileExists()
    {
        // Arrange
        var challenge = new Challenge
        {
            Id = Guid.NewGuid(),
            Name = F.Lorem.Word(),
            Description = F.Lorem.Sentence(),
            Points = F.Random.Int(1, 100),
            DeadlineEnabled = F.Random.Bool(),
            Deadline = DateTime.UtcNow,
            MaxAttempts = F.Random.Int(1, 10),
            Flags = F.Lorem.Words().ToList()
        };
        DbContext.Add(challenge);
        await DbContext.SaveChangesAsync();

        var challengeFile = new ChallengeFile
        {
            Id = Guid.NewGuid(),
            ChallengeId = challenge.Id,
            FileName = F.System.FileName(),
            ContentType = F.System.MimeType(),
            Data = Encoding.UTF8.GetBytes(F.Lorem.Text()),
            Challenge = challenge
        };
        DbContext.Add(challengeFile);

        await DbContext.SaveChangesAsync();

        var challengeFileData = new ChallengeFileDataResponse(
            FileName: challengeFile.FileName,
            ContentType: challengeFile.ContentType,
            Data: challengeFile.Data);

        // Act
        var getChallengeFile = await Sender.Send(new GetChallengeFile.Query(challengeFile.Id));

        // Assert
        getChallengeFile.Should().NotBeNull();
        getChallengeFile.Should().BeOfType<Result<ChallengeFileDataResponse>>();
        getChallengeFile.Value.Should().BeEquivalentTo(challengeFileData);
    }

    [Fact]
    public async Task Handle_Should_NotGetChallengeFile_WhenChallengeFileDoesNotExists()
    {
        // Act
        var getChallengeFile = new GetChallengeFile.Query(Guid.NewGuid());
        var challengeFile = await Sender.Send(getChallengeFile);

        // Assert
        challengeFile.IsSuccess.Should().BeFalse();
    }
}