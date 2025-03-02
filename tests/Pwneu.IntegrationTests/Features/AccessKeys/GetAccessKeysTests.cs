using FluentAssertions;
using Pwneu.Api.Common;
using Pwneu.Api.Contracts;
using Pwneu.Api.Entities;
using Pwneu.Api.Features.AccessKeys;

namespace Pwneu.IntegrationTests.Features.AccessKeys;

[Collection(nameof(IntegrationTestCollection))]
public class GetAccessKeysTests(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetAccessKeys()
    {
        // Arrange
        foreach (var _ in Enumerable.Range(1, 3))
        {
            // Arrange
            var accessKey = AccessKey.Create(true, true, DateTime.UtcNow.AddDays(1));
            DbContext.Add(accessKey);
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
