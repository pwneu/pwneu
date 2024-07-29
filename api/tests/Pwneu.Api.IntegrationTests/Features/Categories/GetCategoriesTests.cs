using FluentAssertions;
using Pwneu.Api.Features.Categories;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.IntegrationTests.Features.Categories;

[Collection(nameof(IntegrationTestCollection))]
public class GetCategoriesTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetCategories()
    {
        // Arrange
        foreach (var unused in Enumerable.Range(1, 3))
        {
            var id = Guid.NewGuid();
            DbContext.Add(new Category
            {
                Id = id,
                Name = F.Lorem.Word(),
                Description = F.Lorem.Sentence(),
            });
            await DbContext.SaveChangesAsync();
        }

        // Act
        var getCategories = new GetCategories.Query();
        var getCategoriesResult = await Sender.Send(getCategories);

        // Assert
        getCategoriesResult.IsSuccess.Should().BeTrue();
        getCategoriesResult.Should().BeOfType<Result<PagedList<CategoryResponse>>>();
    }
}