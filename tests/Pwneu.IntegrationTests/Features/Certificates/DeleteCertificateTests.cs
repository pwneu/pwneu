using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Entities;
using Pwneu.Api.Features.Certificates;
using System.Text;

namespace Pwneu.IntegrationTests.Features.Certificates;

[Collection(nameof(IntegrationTestCollection))]
public class DeleteCertificateTests(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotDeleteCertificate_WhenUserDoesNotExists()
    {
        // Arrange
        await DbContext.Certificates.ExecuteDeleteAsync();

        // Act
        var deleteCertificate = await Sender.Send(
            new DeleteCertificate.Command(Guid.NewGuid().ToString())
        );

        // Assert
        deleteCertificate.IsSuccess.Should().BeFalse();
        deleteCertificate.Error.Should().Be(DeleteCertificate.UserNotFound);
    }

    [Fact]
    public async Task Handle_Should_NotDeleteCertificate_WhenUserDoesNotHaveACertificate()
    {
        // Arrange
        await DbContext.Certificates.ExecuteDeleteAsync();

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
        await DbContext.Certificates.ExecuteDeleteAsync();

        DbContext.Add(
            Certificate.Create(
                TestUser.Id,
                F.Lorem.Word(),
                PdfMimeType,
                Encoding.UTF8.GetBytes(F.Lorem.Word())
            )
        );

        await DbContext.SaveChangesAsync();

        // Act
        var deleteCertificate = await Sender.Send(new DeleteCertificate.Command(TestUser.Id));

        // Assert
        deleteCertificate.IsSuccess.Should().BeTrue();
    }
}
