using FluentAssertions;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
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

    [Fact]
    public async Task Handle_Should_DeleteCategory_WhenCategoryExists()
    {
        // Arrange
        var category = await AddValidCategoryToDatabaseAsync();

        // Act
        var deleteCategory = await Sender.Send(
            new DeleteCategory.Command(category.Id, string.Empty, string.Empty)
        );
        var deletedCategory = DbContext.Categories.FirstOrDefault(c => c.Id == category.Id);

        // Assert
        deleteCategory.IsSuccess.Should().BeTrue();
        deletedCategory.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_InvalidateCache_WhenCategoryWasDeleted()
    {
        // Arrange
        var category = await AddValidCategoryToDatabaseAsync();
        await Cache.SetAsync(CacheKeys.Categories(), new List<CategoryResponse>());

        // Act
        await Sender.Send(new DeleteCategory.Command(category.Id, string.Empty, string.Empty));

        // Assert
        var categoriesCache = await Cache.GetOrDefaultAsync<List<CategoryResponse>>(
            CacheKeys.Categories()
        );
        categoriesCache.Should().BeNull();
    }
}
