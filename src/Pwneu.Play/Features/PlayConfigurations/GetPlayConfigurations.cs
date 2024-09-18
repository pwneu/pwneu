using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.Features.PlayConfigurations;

public static class GetPlayConfigurations
{
    public record Query : IRequest<Result<IEnumerable<PlayConfigurationResponse>>>;

    internal sealed class Handler(ApplicationDbContext context)
        : IRequestHandler<Query, Result<IEnumerable<PlayConfigurationResponse>>>
    {
        public async Task<Result<IEnumerable<PlayConfigurationResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var playConfigurations = await context
                .PlayConfigurations
                .Select(pc => new PlayConfigurationResponse
                {
                    Key = pc.Key,
                    Value = pc.Value,
                })
                .ToListAsync(cancellationToken);

            return playConfigurations;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("configurations", async (ISender sender) =>
                {
                    var query = new Query();
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(PlayConfigurations));
        }
    }
}