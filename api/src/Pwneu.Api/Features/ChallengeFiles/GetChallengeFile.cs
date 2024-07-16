using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Common;
using Pwneu.Api.Shared.Contracts;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;

namespace Pwneu.Api.Features.ChallengeFiles;

public class GetChallengeFile
{
    public record Query(Guid Id) : IRequest<Result<ChallengeFileDataResponse>>;

    internal sealed class Handler(ApplicationDbContext context)
        : IRequestHandler<Query, Result<ChallengeFileDataResponse>>
    {
        public async Task<Result<ChallengeFileDataResponse>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var challengeFileResponse = await context
                .ChallengeFiles
                .Where(cf => cf.Id == request.Id)
                .Select(cf => new ChallengeFileDataResponse(cf.FileName, cf.ContentType, cf.Data))
                .FirstOrDefaultAsync(cancellationToken);

            if (challengeFileResponse is null)
                return Result.Failure<ChallengeFileDataResponse>(new Error("GetChallengeFile.Null",
                    "The challenge file with the specified ID was not found"));

            return challengeFileResponse;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            // TODO: Find proper url for getting challenge files
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