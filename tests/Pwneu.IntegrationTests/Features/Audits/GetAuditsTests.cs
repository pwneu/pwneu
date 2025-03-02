using FluentAssertions;
using Pwneu.Api.Common;
using Pwneu.Api.Contracts;
using Pwneu.Api.Entities;
using Pwneu.Api.Features.Audits;

namespace Pwneu.IntegrationTests.Features.Audits;

[Collection(nameof(IntegrationTestCollection))]
public class GetAuditsTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_GetAudits()
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
        var getAuditsQuery = new GetAudits.Query();
        var getAuditsResult = await Sender.Send(getAuditsQuery);

        // Assert
        getAuditsResult.IsSuccess.Should().BeTrue();
        getAuditsResult.Should().BeOfType<Result<PagedList<AuditResponse>>>();
        getAuditsResult.Value.Should().NotBeNull();
        getAuditsResult.Value.Items.Should().HaveCount(3);
    }
}
