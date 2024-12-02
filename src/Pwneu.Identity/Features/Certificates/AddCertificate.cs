using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;

namespace Pwneu.Identity.Features.Certificates;

public static class AddCertificate
{
    public record Command(
        string UserId,
        string FileName,
        long FileSize,
        string ContentType,
        byte[] Data,
        string UploaderId,
        string UploaderName) : IRequest<Result<Guid>>;

    private const long MaxFileSize = 30 * 1024 * 1024;

    private static readonly Error UserNotFound = new(
        "AddCertificate.UserNotFound",
        "The user with the specified ID was not found");

    public static readonly Error CertificateAlreadyExists = new(
        "AddCertificate.CertificateAlreadyExists",
        "The user already has a certificate");

    private static readonly Error FileSizeLimit = new(
        "AddCertificate.FileSizeLimit",
        $"File size exceeds the limit of {MaxFileSize} megabytes");

    internal sealed class Handler(ApplicationDbContext context, ILogger<Handler> logger, IValidator<Command> validator)
        : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(new Error("AddCertificate.Validation", validationResult.ToString()));

            if (request.FileSize > MaxFileSize)
                return Result.Failure<Guid>(FileSizeLimit);

            var userExists = await context
                .Users
                .Where(u => u.Id == request.UserId)
                .AnyAsync(cancellationToken);

            if (!userExists)
                return Result.Failure<Guid>(UserNotFound);

            var certificateAlreadyExists = await context
                .Certificates
                .Where(c => c.UserId == request.UserId)
                .AnyAsync(cancellationToken);

            if (certificateAlreadyExists)
                return Result.Failure<Guid>(CertificateAlreadyExists);

            var certificate = new Certificate
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                FileName = request.FileName,
                ContentType = request.ContentType,
                Data = request.Data
            };

            context.Add(certificate);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Certificate ({CertificateId}) added for user ({UserId}) by {UploaderName} ({UploaderId})",
                certificate.Id,
                request.UserId,
                request.UploaderName,
                request.UploaderId);

            return certificate.Id;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("users/{id:Guid}/certificate",
                    async (Guid id, IFormFile file, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var uploaderId = claims.GetLoggedInUserId<string>();
                        if (uploaderId is null) return Results.BadRequest();

                        var uploaderName = claims.GetLoggedInUserName();
                        if (uploaderName is null) return Results.BadRequest();

                        using var memoryStream = new MemoryStream();
                        await file.CopyToAsync(memoryStream);

                        var command = new Command(
                            UserId: id.ToString(),
                            FileName: file.FileName,
                            FileSize: file.Length,
                            ContentType: file.ContentType,
                            Data: memoryStream.ToArray(),
                            UploaderId: uploaderId,
                            UploaderName: uploaderName);

                        var result = await sender.Send(command);

                        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
                    })
                .DisableAntiforgery()
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Certificates));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.UserId)
                .NotEmpty()
                .WithMessage("User ID is required.")
                .MaximumLength(36)
                .WithMessage("User ID must not exceed 36 characters.");

            RuleFor(c => c.FileName)
                .NotEmpty()
                .WithMessage("Filename is required.")
                .MaximumLength(100)
                .WithMessage("Filename must not exceed 100 characters.")
                .Must(HaveValidFileExtension)
                .WithMessage("The file must have a .pdf extension.")
                .Must(BeAValidFileName)
                .WithMessage("Filename contains invalid characters.");

            RuleFor(c => c.ContentType)
                .NotEmpty()
                .WithMessage("Content type is required.")
                .MaximumLength(100)
                .WithMessage("Content type must not exceed 100 characters.")
                .Must(BePdfContentType)
                .WithMessage("The file must be a PDF.");
        }

        private static bool BeAValidFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return !fileName.Any(ch => invalidChars.Contains(ch));
        }

        private static bool HaveValidFileExtension(string fileName)
        {
            return fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        }

        private static bool BePdfContentType(string contentType)
        {
            return contentType == "application/pdf";
        }
    }
}