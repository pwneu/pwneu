using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.Features.Artifacts;

public static class AddArtifact
{
    public record Command(
        Guid ChallengeId,
        string FileName,
        string ContentType,
        byte[] Data) : IRequest<Result<Guid>>;

    private static readonly Error NoChallenge = new("AddArtifact.NoChallenge", "No challenge found");

    internal sealed class Handler(ApplicationDbContext context) : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
        {
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

                    var command = new Command(id, file.FileName, file.ContentType, memoryStream.ToArray());

                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
                })
                .DisableAntiforgery() // TODO -- Check for better ways to fix anti-forgery exception
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Artifacts));
        }
    }
}