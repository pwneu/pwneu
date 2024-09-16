using FluentAssertions;
using Pwneu.Identity.Features.AccessKeys;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Identity.IntegrationTests.Features.AccessKeys;

[Collection(nameof(IntegrationTestCollection))]
public class DeleteAccessKeyTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
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
        var accessKeyId = Guid.NewGuid();
        DbContext.Add(new AccessKey
        {
            Id = accessKeyId,
            CanBeReused = true,
            ForManager = true,
            Expiration = DateTime.UtcNow.AddDays(1),
        });
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
        var accessKeyId = Guid.NewGuid();
        DbContext.Add(new AccessKey
        {
            Id = accessKeyId,
            CanBeReused = true,
            ForManager = true,
            Expiration = DateTime.UtcNow.AddDays(1),
        });
        await DbContext.SaveChangesAsync();

        // Act
        await Cache.SetAsync(Keys.AccessKeys(), new List<AccessKeyResponse>());
        await Sender.Send(new DeleteAccessKey.Command(accessKeyId));
        var accessKeyCache = Cache.GetOrDefault<IEnumerable<AccessKeyResponse>>(Keys.AccessKeys());

        // Assert
        accessKeyCache.Should().BeNull();
    }
}