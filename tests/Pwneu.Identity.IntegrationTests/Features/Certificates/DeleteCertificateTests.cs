using System.Text;
using FluentAssertions;
using Pwneu.Identity.Features.Certificates;
using Pwneu.Identity.Shared.Entities;

namespace Pwneu.Identity.IntegrationTests.Features.Certificates;

[Collection(nameof(IntegrationTestCollection))]
public class DeleteCertificateTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotDeleteCertificate_WhenUserDoesNotExists()
    {
        // Act
        var deleteCertificate = await Sender.Send(new DeleteCertificate.Command(Guid.NewGuid().ToString()));

        // Assert
        deleteCertificate.IsSuccess.Should().BeFalse();
        deleteCertificate.Error.Should().Be(DeleteCertificate.UserNotFound);
    }

    [Fact]
    public async Task Handle_Should_NotDeleteCertificate_WhenUserDoesNotHaveACertificate()
    {
        // Act
        var deleteCertificate = await Sender.Send(new DeleteCertificate.Command(TestUser.Id));

        // Assert
        deleteCertificate.IsSuccess.Should().BeFalse();
        deleteCertificate.Error.Should().Be(DeleteCertificate.CertificateNotFound);
    }

    [Fact]
    public async Task Handle_Should_DeleteCertificate_WhenUserHasACertificate()
    {
        // Arrange
        DbContext.Add(new Certificate
        {
            Id = Guid.NewGuid(),
            UserId = TestUser.Id,
            ContentType = "application/pdf",
            Data = Encoding.UTF8.GetBytes(F.Lorem.Word())
        });

        await DbContext.SaveChangesAsync();

        // Act
        var deleteCertificate = await Sender.Send(new DeleteCertificate.Command(TestUser.Id));

        // Assert
        deleteCertificate.IsSuccess.Should().BeTrue();
    }
}