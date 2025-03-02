using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Features.Certificates;
using System.Text;

namespace Pwneu.IntegrationTests.Features.Certificates;

[Collection(nameof(IntegrationTestCollection))]
public class CheckCertificateStatusTests(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_Fail_WhenUserDoesNotExists()
    {
        // Arrange
        await DbContext.Certificates.ExecuteDeleteAsync();
        await DbContext.SetConfigurationValueAsync(ConfigurationKeys.IsCertificationEnabled, true);
        await Cache.RemoveAsync(CacheKeys.IsCertificationEnabled());

        // Act
        var checkCertificateStatus = await Sender.Send(
            new CheckCertificateStatus.Query(Guid.NewGuid().ToString(), TestUser.Id)
        );

        // Assert
        checkCertificateStatus.IsSuccess.Should().BeFalse();
        checkCertificateStatus.Error.Should().Be(CheckCertificateStatus.UserNotFound);
    }

    [Fact]
    public async Task Handle_Should_ReturnWithoutCertificate_WhenUserDoesNotHaveACertificate()
    {
        // Arrange
        await DbContext.Certificates.ExecuteDeleteAsync();
        await DbContext.SetConfigurationValueAsync(ConfigurationKeys.IsCertificationEnabled, true);
        await Cache.RemoveAsync(CacheKeys.IsCertificationEnabled());

        // Act
        var checkCertificateStatus = await Sender.Send(
            new CheckCertificateStatus.Query(TestUser.Id, TestUser.Id)
        );

        // Assert
        checkCertificateStatus.IsSuccess.Should().BeTrue();
        checkCertificateStatus.Value.Should().Be(CertificateStatus.WithoutCertificate);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotAllowed_WhenCertificationIsDisabled()
    {
        // Arrange
        await DbContext.Certificates.ExecuteDeleteAsync();
        await DbContext.SetConfigurationValueAsync(ConfigurationKeys.IsCertificationEnabled, false);
        await Cache.RemoveAsync(CacheKeys.IsCertificationEnabled());

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
        var checkCertificateStatus = await Sender.Send(
            new CheckCertificateStatus.Query(TestUser.Id, TestUser.Id)
        );

        // Assert
        checkCertificateStatus.IsSuccess.Should().BeTrue();
        checkCertificateStatus.Value.Should().Be(CertificateStatus.NotAllowed);
    }

    [Fact]
    public async Task Handle_Should_ReturnWithCertificate_WhenUserHasACertificate()
    {
        // Arrange
        await DbContext.Certificates.ExecuteDeleteAsync();
        await DbContext.SetConfigurationValueAsync(ConfigurationKeys.IsCertificationEnabled, true);
        await Cache.RemoveAsync(CacheKeys.IsCertificationEnabled());

        DbContext.Add(
            Certificate.Create(
                TestUser.Id,
                F.Lorem.Word(),
                PdfMimeType,
                Encoding.UTF8.GetBytes(F.Lorem.Word())
            )
        );

        await DbContext.SaveChangesAsync();

        await DbContext.SaveChangesAsync();

        // Act
        var checkCertificateStatus = await Sender.Send(
            new CheckCertificateStatus.Query(TestUser.Id, TestUser.Id)
        );

        // Assert
        checkCertificateStatus.IsSuccess.Should().BeTrue();
        checkCertificateStatus.Value.Should().Be(CertificateStatus.WithCertificate);
    }
}
