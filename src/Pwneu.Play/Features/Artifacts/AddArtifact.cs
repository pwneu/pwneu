using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Artifacts;

public static class AddArtifact
{
    public record Command(
        Guid ChallengeId,
        string FileName,
        long FileSize,
        string ContentType,
        byte[] Data,
        string UserId,
        string UserName) : IRequest<Result<Guid>>;

    private const long MaxFileSize = 30 * 1024 * 1024;

    private static readonly Error NoChallenge = new("AddArtifact.NoChallenge", "No challenge found");

    private static readonly Error FileSizeLimit = new(
        "AddArtifact.FileSizeLimit",
        $"File size exceeds the limit of {MaxFileSize} megabytes");

    internal sealed class Handler(
        ApplicationDbContext context,
        IFusionCache cache,
        ILogger<Handler> logger,
        IValidator<Command> validator)
        : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(new Error("AddArtifact.Validation", validationResult.ToString()));

            if (request.FileSize > MaxFileSize)
                return Result.Failure<Guid>(FileSizeLimit);

            var challenge = await context
                .Challenges
                .Where(c => c.Id == request.ChallengeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (challenge is null)
                return Result.Failure<Guid>(NoChallenge);

            var artifact = new Artifact
            {
                Id = Guid.NewGuid(),
                ChallengeId = challenge.Id,
                FileName = request.FileName,
                ContentType = request.ContentType,
                Data = request.Data,
                Challenge = challenge
            };

            context.Add(artifact);

            await context.SaveChangesAsync(cancellationToken);
            await cache.RemoveAsync(Keys.ChallengeDetails(artifact.ChallengeId), token: cancellationToken);

            logger.LogInformation(
                "Artifact ({ArtifactId}) added on challenge ({ChallengeId}) by {UserName} ({UserId})",
                artifact.Id,
                request.ChallengeId,
                request.UserName,
                request.UserId);

            return artifact.Id;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("challenges/{id:Guid}/artifacts",
                    async (Guid id, IFormFile file, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null) return Results.BadRequest();

                        var userName = claims.GetLoggedInUserName();
                        if (userName is null) return Results.BadRequest();

                        using var memoryStream = new MemoryStream();
                        await file.CopyToAsync(memoryStream);

                        var command = new Command(id, file.FileName, file.Length, file.ContentType,
                            memoryStream.ToArray(), userId, userName);

                        var result = await sender.Send(command);

                        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
                    })
                .DisableAntiforgery()
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Artifacts));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.FileName)
                .NotEmpty()
                .WithMessage("Filename is required.")
                .MaximumLength(100)
                .WithMessage("Filename must not exceed 100 characters.")
                .Must(BeAValidFileName)
                .WithMessage("Filename contains invalid characters.");

            RuleFor(c => c.ContentType)
                .NotEmpty()
                .WithMessage("Content type is required.")
                .MaximumLength(100)
                .WithMessage("Content type must not exceed 100 characters.");
        }

        private static bool BeAValidFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return !fileName.Any(ch => invalidChars.Contains(ch));
        }
    }
}