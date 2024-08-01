using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.Features.ChallengeFiles;

public static class AddChallengeFile
{
    public record Command(
        Guid ChallengeId,
        string FileName,
        string ContentType,
        byte[] Data) : IRequest<Result<Guid>>;

    private static readonly Error NoChallenge = new("AddChallengeFile.NoChallenge", "No challenge found");

    internal sealed class Handler(ApplicationDbContext context) : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
        {
            var challenge = await context
                .Challenges
                .Where(c => c.Id == request.ChallengeId)
                .Include(c => c.ChallengeFiles)
                .FirstOrDefaultAsync(cancellationToken);

            if (challenge is null)
                return Result.Failure<Guid>(NoChallenge);

            var challengeFile = new ChallengeFile
            {
                Id = Guid.NewGuid(),
                ChallengeId = challenge.Id,
                FileName = request.FileName,
                ContentType = request.ContentType,
                Data = request.Data,
                Challenge = challenge
            };

            context.Add(challengeFile);

            await context.SaveChangesAsync(cancellationToken);

            return challengeFile.Id;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("challenges/{id:Guid}/files", async (Guid id, IFormFile file, ISender sender) =>
                {
                    using var memoryStream = new MemoryStream();
                    await file.CopyToAsync(memoryStream);

                    var command = new Command(id, file.FileName, file.ContentType, memoryStream.ToArray());

                    var result = await sender.Send(command);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
                })
                .DisableAntiforgery() // TODO: Check for better ways to fix anti-forgery exception
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(ChallengeFiles));
        }
    }
}