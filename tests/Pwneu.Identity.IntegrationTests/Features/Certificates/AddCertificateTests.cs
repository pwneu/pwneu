using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Pwneu.Identity.Features.Certificates;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;

namespace Pwneu.Identity.IntegrationTests.Features.Certificates;

[Collection(nameof(IntegrationTestCollection))]
public class AddCertificateTests(IntegrationTestsWebAppFactory factory) : BaseIntegrationTest(factory)
{
    private const string PdfMimeType = "application/pdf";
    private const string PdfFileExtension = ".pdf";

    [Fact]
    public async Task Handle_Should_NotAddCertificate_WhenUserDoesNotExist()
    {
        // Arrange
        var fileContent = Encoding.UTF8.GetBytes(F.Lorem.Word());
        var formStream = new MemoryStream(fileContent);
        var file = new FormFile(formStream, 0, fileContent.Length, F.Lorem.Word(),
            F.System.FileName() + PdfFileExtension)
        {
            Headers = new HeaderDictionary(),
            ContentType = PdfMimeType
        };

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);

        // Act
        var addCertificate = await Sender.Send(new AddCertificate.Command(
            UserId: Guid.NewGuid().ToString(),
            FileName: file.FileName,
            FileSize: file.Length,
            ContentType: file.ContentType,
            Data: stream.ToArray(),
            UploaderId: string.Empty,
            UploaderName: string.Empty));

        // Assert
        addCertificate.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_NotAddCertificate_WhenUserAlreadyHasCertificate()
    {
        // Arrange
        DbContext.Add(new Certificate
        {
            Id = Guid.NewGuid(),
            UserId = TestUser.Id,
            FileName = F.Lorem.Word(),
            ContentType = PdfMimeType,
            Data = Encoding.UTF8.GetBytes(F.Lorem.Word())
        });

        await DbContext.SaveChangesAsync();

        var fileContent = Encoding.UTF8.GetBytes(F.Lorem.Word());
        var formStream = new MemoryStream(fileContent);
        var file = new FormFile(formStream, 0, fileContent.Length, F.Lorem.Word(),
            F.System.FileName() + PdfFileExtension)
        {
            Headers = new HeaderDictionary(),
            ContentType = PdfMimeType
        };

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);

        // Act
        var addCertificate = await Sender.Send(new AddCertificate.Command(
            UserId: TestUser.Id,
            FileName: file.FileName,
            FileSize: file.Length,
            ContentType: file.ContentType,
            Data: stream.ToArray(),
            UploaderId: string.Empty,
            UploaderName: string.Empty));

        // Assert
        addCertificate.IsSuccess.Should().BeFalse();
        addCertificate.Error.Should().Be(AddCertificate.CertificateAlreadyExists);
    }

    [Fact]
    public async Task Handle_Should_NotAddCertificate_WhenFileIsNotPdf()
    {
        // Arrange
        var fileContent = Encoding.UTF8.GetBytes(F.Lorem.Word());
        var formStream = new MemoryStream(fileContent);
        var file = new FormFile(formStream, 0, fileContent.Length, F.Lorem.Word(),
            F.System.FileName())
        {
            Headers = new HeaderDictionary(),
            ContentType = F.System.MimeType()
        };

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);

        // Act
        var addCertificate = await Sender.Send(new AddCertificate.Command(
            UserId: TestUser.Id,
            FileName: file.FileName,
            FileSize: file.Length,
            ContentType: file.ContentType,
            Data: stream.ToArray(),
            UploaderId: string.Empty,
            UploaderName: string.Empty));

        // Assert
        addCertificate.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_NotAddCertificate_WhenFileContentTypeIsNotPdf()
    {
        // Arrange
        var fileContent = Encoding.UTF8.GetBytes(F.Lorem.Word());
        var formStream = new MemoryStream(fileContent);
        var file = new FormFile(formStream, 0, fileContent.Length, F.Lorem.Word(),
            F.System.FileName() + PdfFileExtension)
        {
            Headers = new HeaderDictionary(),
            ContentType = F.System.MimeType()
        };

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);

        // Act
        var addCertificate = await Sender.Send(new AddCertificate.Command(
            UserId: TestUser.Id,
            FileName: file.FileName,
            FileSize: file.Length,
            ContentType: file.ContentType,
            Data: stream.ToArray(),
            UploaderId: string.Empty,
            UploaderName: string.Empty));

        // Assert
        addCertificate.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_AddCertificate_WhenUserAndCertificateExists()
    {
        // Arrange
        var fileContent = Encoding.UTF8.GetBytes(F.Lorem.Word());
        var formStream = new MemoryStream(fileContent);
        var file = new FormFile(formStream, 0, fileContent.Length, F.Lorem.Word(),
            F.System.FileName() + PdfFileExtension)
        {
            Headers = new HeaderDictionary(),
            ContentType = PdfMimeType
        };

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);

        // Act
        var addCertificate = await Sender.Send(new AddCertificate.Command(
            UserId: TestUser.Id,
            FileName: file.FileName,
            FileSize: file.Length,
            ContentType: file.ContentType,
            Data: stream.ToArray(),
            UploaderId: string.Empty,
            UploaderName: string.Empty));

        var certificate = DbContext.Certificates.FirstOrDefault(c => c.Id == addCertificate.Value);

        // Assert
        addCertificate.Should().BeOfType<Result<Guid>>();
        addCertificate.IsSuccess.Should().BeTrue();
        certificate.Should().NotBeNull();
    }
}