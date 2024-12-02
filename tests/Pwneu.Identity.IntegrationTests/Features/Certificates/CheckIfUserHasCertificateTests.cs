using System.Text;
using FluentAssertions;
using Pwneu.Identity.Features.Certificates;
using Pwneu.Identity.Shared.Entities;

namespace Pwneu.Identity.IntegrationTests.Features.Certificates;

[Collection(nameof(IntegrationTestCollection))]
public class CheckIfUserHasCertificateTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    [Fact]
    public async Task Handle_Should_Fail_WhenUserDoesNotExists()
    {
        // Act
        var getCertificate = await Sender.Send(new CheckIfUserHasCertificate.Query(Guid.NewGuid().ToString()));

        // Assert
        getCertificate.IsSuccess.Should().BeFalse();
        getCertificate.Error.Should().Be(CheckIfUserHasCertificate.UserNotFound);
    }

    [Fact]
    public async Task Handle_Should_ReturnFalse_WhenUserDoesNotHaveACertificate()
    {
        // Act
        var getCertificate = await Sender.Send(new CheckIfUserHasCertificate.Query(TestUser.Id));

        // Assert
        getCertificate.IsSuccess.Should().BeTrue();
        getCertificate.Value.Should().Be(false);
    }

    [Fact]
    public async Task Handle_Should_ReturnTrue_WhenUserHasACertificate()
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
        var getCertificate = await Sender.Send(new CheckIfUserHasCertificate.Query(TestUser.Id));

        // Assert
        getCertificate.IsSuccess.Should().BeTrue();
        getCertificate.Value.Should().Be(true);
    }
}