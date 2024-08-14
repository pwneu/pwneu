using FluentAssertions;
using Pwneu.Play.Features.Categories;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.IntegrationTests.Features.Categories;

[Collection(nameof(IntegrationTestCollection))]
public class DeleteCategoryTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotDeleteCategory_WhenCategoryDoesNotExists()
    {
        // Arrange
        var challengeId = Guid.NewGuid();

        // Act
        var deleteChallenge = await Sender.Send(new DeleteCategory.Command(challengeId));

        // Assert
        deleteChallenge.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_DeleteCategory_WhenCategoryExists()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        DbContext.Add(new Category
        {
            Id = categoryId,
            Name = F.Lorem.Word(),
            Description = F.Lorem.Sentence()
        });
        await DbContext.SaveChangesAsync();

        // Act
        var deleteCategory = await Sender.Send(new DeleteCategory.Command(categoryId));
        var deletedCategory = DbContext.Challenges.FirstOrDefault(c => c.Id == categoryId);

        // Assert
        deleteCategory.IsSuccess.Should().BeTrue();
        deletedCategory.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_InvalidateCategoryCache()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        DbContext.Add(new Category
        {
            Id = categoryId,
            Name = F.Lorem.Word(),
            Description = F.Lorem.Sentence()
        });
        await DbContext.SaveChangesAsync();

        // Act
        await Sender.Send(new DeleteCategory.Command(categoryId));
        var categoryCache = Cache.GetOrDefault<CategoryResponse>($"{nameof(CategoryResponse)}:{categoryId}");

        // Assert
        categoryCache.Should().BeNull();
    }
}