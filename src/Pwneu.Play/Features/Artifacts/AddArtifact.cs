using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Artifacts;

public static class AddArtifact
{
    public record Command(
        Guid ChallengeId,
        string FileName,
        long FileSize,
        string ContentType,
        byte[] Data) : IRequest<Result<Guid>>;

    private const long MaxFileSize = 30 * 1024 * 1024;

    private static readonly Error NoChallenge = new("AddArtifact.NoChallenge", "No challenge found");

    private static readonly Error FileSizeLimit = new(
        "AddArtifact.FileSizeLimit",
        $"File size exceeds the limit of {MaxFileSize} megabytes");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.FileSize > MaxFileSize)
                return Result.Failure<Guid>(FileSizeLimit);

            var challenge = await context
                .Challenges
                .Where(c => c.Id == request.ChallengeId)
                .Include(c => c.Artifacts)
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

            return artifact.Id;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("challenges/{id:Guid}/artifacts", async (Guid id, IFormFile file, ISender sender) =>
                {
                    using var memoryStream = new MemoryStream();
                    await file.CopyToAsync(memoryStream);

                    var command = new Command(id, file.FileName, file.Length, file.ContentType, memoryStream.ToArray());

                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
                })
                .DisableAntiforgery()
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Artifacts));
        }
    }
}