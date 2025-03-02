using FluentAssertions;
using Pwneu.Api.Entities;
using Pwneu.Api.Features.Audits;

namespace Pwneu.IntegrationTests.Features.Audits;

[Collection(nameof(IntegrationTestCollection))]
public class DeleteAuditTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotDeleteAudit_WhenAuditDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.CreateVersion7();

        // Act
        var deleteAuditResult = await Sender.Send(new DeleteAudit.Command(nonExistentId));

        // Assert
        deleteAuditResult.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_DeleteAudit_WhenAuditExists()
    {
        // Arrange
        var audit = Audit.Create(
            F.Random.Guid().ToString(),
            F.Internet.UserName(),
            F.Lorem.Sentence()
        );
        await DbContext.Audits.AddAsync(audit);
        await DbContext.SaveChangesAsync();

        // Act
        var deleteAuditResult = await Sender.Send(new DeleteAudit.Command(audit.Id));
        var deletedAudit = DbContext.Audits.FirstOrDefault(a => a.Id == audit.Id);

        // Assert
        deleteAuditResult.IsSuccess.Should().BeTrue();
        deletedAudit.Should().BeNull();
    }
}
