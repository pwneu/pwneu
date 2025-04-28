﻿using FluentAssertions;
using Pwneu.Api.Common;
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
}
