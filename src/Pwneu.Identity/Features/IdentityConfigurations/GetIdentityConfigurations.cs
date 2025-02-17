using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Identity.Features.IdentityConfigurations;

public static class GetIdentityConfigurations
{
    public record Query : IRequest<Result<IEnumerable<IdentityConfigurationsResponse>>>;

    internal sealed class Handler(ApplicationDbContext context)
        : IRequestHandler<Query, Result<IEnumerable<IdentityConfigurationsResponse>>>
    {
        public async Task<Result<IEnumerable<IdentityConfigurationsResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            var identityConfigurations = await context
                .IdentityConfigurations
                .Select(pc => new IdentityConfigurationsResponse
                {
                    Key = pc.Key,
                    Value = pc.Value,
                })
                .ToListAsync(cancellationToken);

            return identityConfigurations;
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
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(IdentityConfigurations));
        }
    }
}