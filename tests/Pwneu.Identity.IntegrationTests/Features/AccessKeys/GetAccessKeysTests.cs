using FluentAssertions;
using Pwneu.Identity.Features.AccessKeys;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Identity.IntegrationTests.Features.AccessKeys;

[Collection(nameof(IntegrationTestCollection))]
public class GetAccessKeysTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetAccessKeys()
    {
        // Arrange
        foreach (var unused in Enumerable.Range(1, 3))
        {
            var id = Guid.NewGuid();
            DbContext.Add(new AccessKey
            {
                Id = id,
                CanBeReused = true,
                ForManager = true,
                Expiration = DateTime.UtcNow.AddDays(1),
            });
            await DbContext.SaveChangesAsync();
        }

        // Act
        var getAccessKeys = new GetAccessKeys.Query();
        var getAccessKeysResult = await Sender.Send(getAccessKeys);

        // Assert
        getAccessKeysResult.IsSuccess.Should().BeTrue();
        getAccessKeysResult.Should().BeOfType<Result<IEnumerable<AccessKeyResponse>>>();
    }
}