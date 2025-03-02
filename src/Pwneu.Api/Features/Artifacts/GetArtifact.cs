using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Artifacts;

public static class GetArtifact
{
    public record Query(Guid Id) : IRequest<Result<ArtifactDataResponse>>;

    private static readonly Error NotFound = new(
        "GetArtifact.NotFound",
        "The artifact with the specified ID was not found"
    );

    internal sealed class Handler(AppDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<ArtifactDataResponse>>
    {
        public async Task<Result<ArtifactDataResponse>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var artifact = await cache.GetOrSetAsync(
                CacheKeys.ArtifactData(request.Id),
                async _ =>
                    await context
                        .Artifacts.Where(a => a.Id == request.Id)
                        .Select(a => new ArtifactDataResponse
                        {
                            FileName = a.FileName,
                            ContentType = a.ContentType,
                            Data = a.Data,
                        })
                        .FirstOrDefaultAsync(cancellationToken),
                token: cancellationToken
            );

            return artifact ?? Result.Failure<ArtifactDataResponse>(NotFound);
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/artifacts/{id:Guid}",
                    async (Guid id, ISender sender) =>
                    {
                        var query = new Query(id);

                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.NotFound(result.Error)
                            : Results.File(
                                result.Value.Data,
                                result.Value.ContentType,
                                result.Value.FileName
                            );
                    }
                )
                .RequireAuthorization()
                .RequireRateLimiting(RateLimitingPolicies.GetArtifact)
                .WithTags(nameof(Artifacts));
        }
    }
}
