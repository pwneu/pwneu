using FluentAssertions;
using Pwneu.Api.Common;
using Pwneu.Api.Contracts;
using Pwneu.Api.Features.Categories;

namespace Pwneu.IntegrationTests.Features.Categories;

[Collection(nameof(IntegrationTestCollection))]
public class GetAllCategoriesTest(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetCategories()
    {
        // Arrange
        foreach (var _ in Enumerable.Range(1, 3))
        {
            await AddValidCategoryToDatabaseAsync();
        }

        // Act
        var getAllCategories = new GetAllCategories.Query();
        var getAllCategoriesResult = await Sender.Send(getAllCategories);

        // Assert
        getAllCategoriesResult.IsSuccess.Should().BeTrue();
        getAllCategoriesResult.Should().BeOfType<Result<IEnumerable<CategoryResponse>>>();
    }
}
