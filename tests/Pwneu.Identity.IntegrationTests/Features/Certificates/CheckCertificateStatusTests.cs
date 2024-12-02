using System.Text;
using FluentAssertions;
using Pwneu.Identity.Features.Certificates;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Identity.Shared.Extensions;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Identity.IntegrationTests.Features.Certificates;

[Collection(nameof(IntegrationTestCollection))]
public class CheckCertificateStatusTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_Fail_WhenUserDoesNotExists()
    {
        // Act
        var checkCertificateStatus = await Sender.Send(new CheckCertificateStatus.Query(Guid.NewGuid().ToString()));

        // Assert
        checkCertificateStatus.IsSuccess.Should().BeFalse();
        checkCertificateStatus.Error.Should().Be(CheckCertificateStatus.UserNotFound);
    }

    [Fact]
    public async Task Handle_Should_ReturnWithoutCertificate_WhenUserDoesNotHaveACertificate()
    {
        // Arrange
        await DbContext.SetIdentityConfigurationValueAsync(Consts.IsCertificationEnabled, true);

        // Act
        var checkCertificateStatus = await Sender.Send(new CheckCertificateStatus.Query(TestUser.Id));

        // Assert
        checkCertificateStatus.IsSuccess.Should().BeTrue();
        checkCertificateStatus.Value.Should().Be(CertificateStatus.WithoutCertificate);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotAllowed_WhenCertificationIsDisabled()
    {
        // Arrange
        DbContext.Add(new Certificate
        {
            Id = Guid.NewGuid(),
            UserId = TestUser.Id,
            FileName = F.System.FileName(),
            ContentType = "application/pdf",
            Data = Encoding.UTF8.GetBytes(F.Lorem.Word())
        });

        await DbContext.SaveChangesAsync();

        // Act
        var checkCertificateStatus = await Sender.Send(new CheckCertificateStatus.Query(TestUser.Id));

        // Assert
        checkCertificateStatus.IsSuccess.Should().BeTrue();
        checkCertificateStatus.Value.Should().Be(CertificateStatus.NotAllowed);
    }

    [Fact]
    public async Task Handle_Should_ReturnWithCertificate_WhenUserHasACertificate()
    {
        // Arrange
        await DbContext.SetIdentityConfigurationValueAsync(Consts.IsCertificationEnabled, true);

        DbContext.Add(new Certificate
        {
            Id = Guid.NewGuid(),
            UserId = TestUser.Id,
            FileName = F.System.FileName(),
            ContentType = "application/pdf",
            Data = Encoding.UTF8.GetBytes(F.Lorem.Word())
        });

        await DbContext.SaveChangesAsync();

        // Act
        var checkCertificateStatus = await Sender.Send(new CheckCertificateStatus.Query(TestUser.Id));

        // Assert
        checkCertificateStatus.IsSuccess.Should().BeTrue();
        checkCertificateStatus.Value.Should().Be(CertificateStatus.WithCertificate);
    }
}