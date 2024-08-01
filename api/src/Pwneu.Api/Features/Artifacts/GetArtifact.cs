using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Artifacts;

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
            var artifact = await cache.GetOrSetAsync(Keys.Artifact(request.Id), async _ =>
                await context
                    .Artifacts
                    .Where(a => a.Id == request.Id)
                    .Select(a => new ArtifactDataResponse(a.FileName, a.ContentType, a.Data))
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