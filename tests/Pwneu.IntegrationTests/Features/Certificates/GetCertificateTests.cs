using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Features.Certificates;
using System.Text;

namespace Pwneu.IntegrationTests.Features.Certificates;

[Collection(nameof(IntegrationTestCollection))]
public class GetCertificateTests(IntegrationTestsWebAppFactory factory)
    : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotGetUserCertificate_WhenUserDoesNotExists()
    {
        // Arrange
        await DbContext.Certificates.ExecuteDeleteAsync();
        await DbContext.SetConfigurationValueAsync(ConfigurationKeys.IsCertificationEnabled, true);
        await Cache.RemoveAsync(CacheKeys.IsCertificationEnabled());

        // Act
        var getCertificate = await Sender.Send(
            new GetCertificate.Query(Guid.NewGuid().ToString(), TestUser.Id)
        );

        // Assert
        getCertificate.IsSuccess.Should().BeFalse();
        getCertificate.Error.Should().Be(GetCertificate.UserNotFound);
    }

    [Fact]
    public async Task Handle_Should_NotGetUserCertificate_WhenUserDoesNotHaveACertificate()
    {
        // Arrange
        await DbContext.Certificates.ExecuteDeleteAsync();
        await DbContext.SetConfigurationValueAsync(ConfigurationKeys.IsCertificationEnabled, true);
        await Cache.RemoveAsync(CacheKeys.IsCertificationEnabled());

        // Act
        var getCertificate = await Sender.Send(new GetCertificate.Query(TestUser.Id, TestUser.Id));

        // Assert
        getCertificate.IsSuccess.Should().BeFalse();
        getCertificate.Error.Should().Be(GetCertificate.CertificateNotFound);
    }

    [Fact]
    public async Task Handle_Should_NotGetCertificate_WhenCertificationIsDisabled()
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
        var getCertificate = await Sender.Send(new GetCertificate.Query(TestUser.Id, TestUser.Id));

        // Assert
        getCertificate.IsSuccess.Should().BeFalse();
        getCertificate.Error.Should().Be(GetCertificate.NotAllowed);
    }

    [Fact]
    public async Task Handle_Should_GetCertificate_WhenUserAndCertificateExists()
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

        // Act
        var getCertificate = await Sender.Send(new GetCertificate.Query(TestUser.Id, TestUser.Id));

        // Assert
        getCertificate.IsSuccess.Should().BeTrue();
        getCertificate.Should().BeOfType<Result<CertificateResponse>>();
    }
}
