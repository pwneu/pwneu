using FluentAssertions;
using Pwneu.Identity.Features.AccessKeys;
using Pwneu.Shared.Common;

namespace Pwneu.Identity.IntegrationTests.Features.AccessKeys;

[Collection(nameof(IntegrationTestCollection))]
public class CreateAccessKeyTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotCreateAccessKey_WhenCommandIsNotValid()
    {
        // Arrange
        var createAccessKeys = new List<CreateAccessKey.Command>
        {
            new(true, true, DateTime.UtcNow.AddDays(-1)),
            new(true, false, DateTime.UtcNow.AddDays(-1)),
            new(false, true, DateTime.UtcNow.AddDays(-1)),
            new(false, false, DateTime.UtcNow.AddDays(-1))
        };

        // Act
        var createAccessKeyResults = await Task.WhenAll(createAccessKeys
            .Select(invalidAccessKeys => Sender.Send(invalidAccessKeys))
            .ToList());

        // Assert
        foreach (var createAccessKeyResult in createAccessKeyResults)
        {
            createAccessKeyResult.Should().BeOfType<Result<Guid>>();
            createAccessKeyResult.IsSuccess.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Handle_Should_CreateAccessKey_WhenCommandIsValid()
    {
        // Arrange
        var createAccessKey = new CreateAccessKey.Command(true, true, DateTime.UtcNow.AddDays(1));

        // Act
        var createAccessKeyResult = await Sender.Send(createAccessKey);
        var accessKey = DbContext.AccessKeys.FirstOrDefault(c => c.Id == createAccessKeyResult.Value);

        // Assert
        createAccessKeyResult.Should().BeOfType<Result<Guid>>();
        createAccessKeyResult.IsSuccess.Should().BeTrue();
        accessKey.Should().NotBeNull();
        accessKey.Id.Should().Be(createAccessKeyResult.Value);
    }
}