using Bogus;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Features.Categories;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.IntegrationTests.Features.Categories;

[Collection(nameof(IntegrationTestCollection))]
public class UpdateCategoryTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotUpdateCategory_WhenCommandIsNotValid()
    {
        // Arrange
        var categoryIds = new List<Guid>();
        foreach (var unused in Enumerable.Range(1, 2))
        {
            var categoryId = Guid.NewGuid();
            categoryIds.Add(categoryId);
            var category = new Category
            {
                Id = categoryId,
                Name = F.Lorem.Word(),
                Description = F.Lorem.Sentence()
            };
            DbContext.Add(category);
            await DbContext.SaveChangesAsync();
        }

        var updatedCategories = new List<UpdateCategory.Command>
        {
            new(categoryIds[0], string.Empty, F.Lorem.Sentence()),
            new(categoryIds[1], F.Lorem.Word(), string.Empty)
        };

        // Act
        var updateCategories = new List<Result>();
        foreach (var updatedCategory in updatedCategories)
        {
            var updateChallenge = await Sender.Send(updatedCategory);
            updateCategories.Add(updateChallenge);
        }

        // Assert
        foreach (var updateCategory in updateCategories)
            updateCategory.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_NotUpdateUpdateCategory_WhenCategoryDoesNotExists()
    {
        // Act
        var updateCategory = await Sender.Send(new UpdateCategory.Command(
            Id: Guid.NewGuid(),
            Name: F.Lorem.Word(),
            Description: F.Lorem.Sentence()));

        // Assert
        updateCategory.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_GetDifferentCategoryDetails()
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

        var category = new CategoryResponse(categoryId, F.Lorem.Word(), F.Lorem.Sentence(), []);

        // Act
        var faker = new Faker();
        var updateCategory = await Sender.Send(new UpdateCategory.Command(
            Id: categoryId,
            Name: faker.Lorem.Word(),
            Description: faker.Lorem.Sentence()));

        var updatedCategory = await DbContext
            .Categories
            .Where(ctg => ctg.Id == categoryId)
            .Select(ctg => new CategoryResponse(ctg.Id, ctg.Name, ctg.Description, ctg.Challenges
                .Select(c => new ChallengeResponse(c.Id, c.Name))
                .ToList()))
            .FirstOrDefaultAsync();

        // Assert
        updateCategory.IsSuccess.Should().BeTrue();
        updatedCategory.Should().NotBeNull();
        updatedCategory.Should().NotBeEquivalentTo(category);
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
        await Sender.Send(new UpdateCategory.Command(
            Id: categoryId,
            Name: F.Lorem.Word(),
            Description: F.Lorem.Sentence()));

        var categoryCache = Cache.GetOrDefault<CategoryResponse>($"{nameof(CategoryResponse)}:{categoryId}");

        // Assert
        categoryCache.Should().BeNull();
    }
}