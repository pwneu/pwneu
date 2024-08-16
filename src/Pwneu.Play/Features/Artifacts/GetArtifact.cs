using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Artifacts;

/// <summary>
/// Retrieves an artifact by ID
/// </summary>
public static class GetArtifact
{
    public record Query(Guid Id) : IRequest<Result<ArtifactDataResponse>>;

    private static readonly Error NotFound = new("GetArtifact.NotFound",
        "The artifact with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<ArtifactDataResponse>>
    {
        public async Task<Result<ArtifactDataResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var artifact = await cache.GetOrSetAsync(Keys.ArtifactData(request.Id), async _ =>
                await context
                    .Artifacts
                    .Where(a => a.Id == request.Id)
                    .Select(a => new ArtifactDataResponse
                    {
                        FileName = a.FileName,
                        ContentType = a.ContentType,
                        Data = a.Data
                    })
                    .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

            return artifact ?? Result.Failure<ArtifactDataResponse>(NotFound);
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("artifacts/{id:Guid}",
                    async (Guid id, ISender sender) =>
                    {
                        var query = new Query(id);

                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.NotFound(result.Error)
                            : Results.File(result.Value.Data, result.Value.ContentType, result.Value.FileName);
                    })
                .RequireAuthorization()
                .WithTags(nameof(Artifacts));
        }
    }
}