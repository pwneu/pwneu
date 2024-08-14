using FluentAssertions;
using Pwneu.Play.Features.Categories;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.IntegrationTests.Features.Categories;

[Collection(nameof(IntegrationTestCollection))]
public class GetCategoryTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetCategory_WhenCategoryExists()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var category = new Category
        {
            Id = categoryId,
            Name = F.Lorem.Word(),
            Description = F.Lorem.Sentence()
        };
        DbContext.Add(category);
        await DbContext.SaveChangesAsync();

        var categoryResponse = new CategoryResponse
        {
            Id = categoryId,
            Name = category.Name,
            Description = category.Description,
            Challenges = new List<ChallengeResponse>()
        };

        // Act
        var getCategory = new GetCategory.Query(categoryId);
        var getCategoryResult = await Sender.Send(getCategory);

        // Assert
        getCategoryResult.IsSuccess.Should().BeTrue();
        getCategoryResult.Should().BeOfType<Result<CategoryResponse>>();
        getCategoryResult.Value.Should().BeEquivalentTo(categoryResponse);
    }

    [Fact]
    public async Task Handle_Should_NotGetCategory_WhenCategoryDoesNotExists()
    {
        // Arrange
        var categoryId = Guid.NewGuid();

        // Act
        var getCategory = new GetCategory.Query(categoryId);
        var getCategoryResult = await Sender.Send(getCategory);

        // Assert
        getCategoryResult.IsSuccess.Should().BeFalse();
    }
}