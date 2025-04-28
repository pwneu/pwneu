using FluentAssertions;
using Pwneu.Api.Features.Categories;

namespace Pwneu.IntegrationTests.Features.Categories;

[Collection(nameof(IntegrationTestCollection))]
public class DeleteCategoryTests(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotDeleteCategory_WhenCategoryDoesNotExists()
    {
        // Arrange
        var challengeId = Guid.CreateVersion7();

        // Act
        var deleteChallenge = await Sender.Send(
            new DeleteCategory.Command(challengeId, string.Empty, string.Empty)
        );

        // Assert
        deleteChallenge.IsSuccess.Should().BeFalse();
    }
}
