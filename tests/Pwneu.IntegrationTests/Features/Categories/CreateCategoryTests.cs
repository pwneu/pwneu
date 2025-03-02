using FluentAssertions;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Features.Categories;

namespace Pwneu.IntegrationTests.Features.Categories;

[Collection(nameof(IntegrationTestCollection))]
public class CreateCategoryTests(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotCreateCategory_WhenCommandIsNotValid()
    {
        // Arrange
        var createCategories = new List<CreateCategory.Command>
        {
            new(string.Empty, F.Lorem.Sentence(), string.Empty, string.Empty),
            new(F.Lorem.Word(), string.Empty, string.Empty, string.Empty),
        };

        // Act
        var createCategoryResults = await Task.WhenAll(
            createCategories.Select(invalidCategory => Sender.Send(invalidCategory)).ToList()
        );

        // Assert
        foreach (var createCategoryResult in createCategoryResults)
        {
            createCategoryResult.Should().BeOfType<Result<Guid>>();
            createCategoryResult.IsSuccess.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Handle_Should_CreateCategory_WhenCommandIsValid()
    {
        // Arrange
        var createCategory = new CreateCategory.Command(
            F.Lorem.Word(),
            F.Lorem.Sentence(),
            string.Empty,
            string.Empty
        );

        // Act
        var createCategoryResult = await Sender.Send(createCategory);
        var category = DbContext.Categories.FirstOrDefault(c => c.Id == createCategoryResult.Value);

        // Assert
        createCategoryResult.Should().BeOfType<Result<Guid>>();
        createCategoryResult.IsSuccess.Should().BeTrue();
        category.Should().NotBeNull();
        category!.Id.Should().Be(createCategoryResult.Value);
    }

    [Fact]
    public async Task Handle_Should_InvalidateCache_WhenCategoryWasCreated()
    {
        // Arrange
        var category = await AddValidCategoryToDatabaseAsync();
        await Cache.SetAsync(CacheKeys.Categories(), new List<CategoryResponse>());

        // Act
        await Sender.Send(
            new CreateCategory.Command(
                F.Lorem.Word(),
                F.Lorem.Sentence(),
                string.Empty,
                string.Empty
            )
        );

        // Assert
        var categoriesCache = await Cache.GetOrDefaultAsync<List<CategoryResponse>>(
            CacheKeys.Categories()
        );
        categoriesCache.Should().BeNull();
    }
}
