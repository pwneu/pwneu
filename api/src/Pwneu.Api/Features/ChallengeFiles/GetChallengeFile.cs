using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.ChallengeFiles;

/// <summary>
/// Retrieves a challenge file by ID
/// </summary>
public static class GetChallengeFile
{
    public record Query(Guid Id) : IRequest<Result<ChallengeFileDataResponse>>;

    private static readonly Error NotFound = new Error("GetChallengeFile.Null",
        "The challenge file with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<ChallengeFileDataResponse>>
    {
        public async Task<Result<ChallengeFileDataResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var challengeFileDataResponse = await cache.GetOrSetAsync($"{nameof(ChallengeFile)}:{request.Id}",
                async _ =>
                {
                    return await context
                        .ChallengeFiles
                        .Where(cf => cf.Id == request.Id)
                        .Select(cf => new ChallengeFileDataResponse(cf.FileName, cf.ContentType, cf.Data))
                        .FirstOrDefaultAsync(cancellationToken);
                }, token: cancellationToken);

            return challengeFileDataResponse ?? Result.Failure<ChallengeFileDataResponse>(NotFound);
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            // TODO -- Find proper url for getting challenge files
            app.MapGet("challenges/files/{id:Guid}",
                    async (Guid id, ISender sender) =>
                    {
                        var query = new Query(id);

                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.NotFound(result.Error)
                            : Results.File(result.Value.Data, result.Value.ContentType, result.Value.FileName);
                    })
                .RequireAuthorization()
                .WithTags(nameof(ChallengeFile));
        }
    }
}