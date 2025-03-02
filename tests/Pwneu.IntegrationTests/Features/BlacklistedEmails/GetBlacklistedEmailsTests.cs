using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Entities;
using Pwneu.Api.Features.BlacklistedEmails;

namespace Pwneu.IntegrationTests.Features.BlacklistedEmails;

[Collection(nameof(IntegrationTestCollection))]
public class GetBlacklistedEmailsTests(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetBlacklistedEmails()
    {
        // Arrange
        await DbContext.BlacklistedEmails.ExecuteDeleteAsync();

        var blacklistedEmails = new List<BlacklistedEmail>
        {
            BlacklistedEmail.Create(F.Internet.Email()),
            BlacklistedEmail.Create(F.Internet.Email()),
            BlacklistedEmail.Create(F.Internet.Email()),
        };

        await DbContext.BlacklistedEmails.AddRangeAsync(blacklistedEmails);
        await DbContext.SaveChangesAsync();

        // Act
        var query = new GetBlacklistedEmails.Query();
        var result = await Sender.Send(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Should().BeOfType<Result<IEnumerable<BlacklistedEmail>>>();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(3);
    }
}
