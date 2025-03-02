using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Entities;
using Pwneu.Api.Features.Audits;

namespace Pwneu.IntegrationTests.Features.Audits;

[Collection(nameof(IntegrationTestCollection))]
public class DeleteAllAuditsTests(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_DeleteAllAudits()
    {
        // Arrange
        var audits = new List<Audit>
        {
            Audit.Create(F.Random.Guid().ToString(), F.Internet.UserName(), F.Lorem.Sentence()),
            Audit.Create(F.Random.Guid().ToString(), F.Internet.UserName(), F.Lorem.Sentence()),
            Audit.Create(F.Random.Guid().ToString(), F.Internet.UserName(), F.Lorem.Sentence()),
        };

        await DbContext.Audits.AddRangeAsync(audits);
        await DbContext.SaveChangesAsync();

        // Act
        var deleteAllAuditsResult = await Sender.Send(new DeleteAllAudits.Command());
        var remainingAudits = await DbContext.Audits.CountAsync();

        // Assert
        deleteAllAuditsResult.IsSuccess.Should().BeTrue();
        remainingAudits.Should().Be(0);
    }
}
