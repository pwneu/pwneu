using System.Text;
using FluentAssertions;
using Pwneu.Identity.Features.Certificates;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Identity.Shared.Extensions;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Identity.IntegrationTests.Features.Certificates;

[Collection(nameof(IntegrationTestCollection))]
public class GetCertificateTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_NotGetUserCertificate_WhenUserDoesNotExists()
    {
        // Arrange
        await DbContext.SetIdentityConfigurationValueAsync(Consts.IsCertificationEnabled, true);

        // Act
        var getCertificate = await Sender.Send(new GetCertificate.Query(Guid.NewGuid().ToString()));

        // Assert
        getCertificate.IsSuccess.Should().BeFalse();
        getCertificate.Error.Should().Be(GetCertificate.UserNotFound);
    }

    [Fact]
    public async Task Handle_Should_NotGetUserCertificate_WhenUserDoesNotHaveACertificate()
    {
        // Arrange
        await DbContext.SetIdentityConfigurationValueAsync(Consts.IsCertificationEnabled, true);

        // Act
        var getCertificate = await Sender.Send(new GetCertificate.Query(TestUser.Id));

        // Assert
        getCertificate.IsSuccess.Should().BeFalse();
        getCertificate.Error.Should().Be(GetCertificate.CertificateNotFound);
    }

    [Fact]
    public async Task Handle_Should_NotGetCertificate_WhenCertificationIsDisabled()
    {
        await Cache.RemoveAsync(Keys.IsCertificationEnabled());
        await DbContext.SetIdentityConfigurationValueAsync(Consts.IsCertificationEnabled, false);

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
        var getCertificate = await Sender.Send(new GetCertificate.Query(TestUser.Id));

        // Assert
        getCertificate.IsSuccess.Should().BeFalse();
        getCertificate.Error.Should().Be(GetCertificate.NotAllowed);
    }

    [Fact]
    public async Task Handle_Should_GetCertificate_WhenUserAndCertificateExists()
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
        var getCertificate = await Sender.Send(new GetCertificate.Query(TestUser.Id));

        // Assert
        getCertificate.IsSuccess.Should().BeTrue();
        getCertificate.Should().BeOfType<Result<CertificateResponse>>();
    }
}