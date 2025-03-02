using FluentAssertions;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Entities;
using Pwneu.Api.Features.AccessKeys;

namespace Pwneu.IntegrationTests.Features.AccessKeys;

[Collection(nameof(IntegrationTestCollection))]
public class DeleteAccessKeyTests(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotDeleteAccessKey_WhenAccessKeyDoesNotExists()
    {
        // Arrange
        var challengeId = Guid.NewGuid();

        // Act
        var deleteChallenge = await Sender.Send(new DeleteAccessKey.Command(challengeId));

        // Assert
        deleteChallenge.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_DeleteAccessKey_WhenAccessKeyExists()
    {
        // Arrange
        var accessKey = AccessKey.Create(true, true, DateTime.UtcNow.AddDays(1));
        var accessKeyId = accessKey.Id;

        DbContext.Add(accessKey);
        await DbContext.SaveChangesAsync();

        // Act
        var deleteAccessKey = await Sender.Send(new DeleteAccessKey.Command(accessKeyId));
        var deletedAccessKey = DbContext.AccessKeys.FirstOrDefault(c => c.Id == accessKeyId);

        // Assert
        deleteAccessKey.IsSuccess.Should().BeTrue();
        deletedAccessKey.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_InvalidateAccessKeyCache()
    {
        // Arrange
        var accessKey = AccessKey.Create(true, true, DateTime.UtcNow.AddDays(1));
        var accessKeyId = accessKey.Id;

        DbContext.Add(accessKey);
        await DbContext.SaveChangesAsync();

        // Act
        await Cache.SetAsync(CacheKeys.AccessKeys(), new List<AccessKeyResponse>());
        await Sender.Send(new DeleteAccessKey.Command(accessKeyId));
        var accessKeyCache = Cache.GetOrDefault<IEnumerable<AccessKeyResponse>>(
            CacheKeys.AccessKeys()
        );

        // Assert
        accessKeyCache.Should().BeNull();
    }
}
