using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;

namespace Pwneu.Api.Features.Configurations;

public class GetConfigurations
{
    public record Query : IRequest<Result<IEnumerable<ConfigurationResponse>>>;

    internal sealed class Handler(AppDbContext context)
        : IRequestHandler<Query, Result<IEnumerable<ConfigurationResponse>>>
    {
        public async Task<Result<IEnumerable<ConfigurationResponse>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            var identityConfigurations = await context
                .Configurations.Select(pc => new ConfigurationResponse
                {
                    Key = pc.Key,
                    Value = pc.Value,
                })
                .ToListAsync(cancellationToken);

            return identityConfigurations;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "identity/configurations",
                    async (ISender sender) =>
                    {
                        var query = new Query();
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .WithTags(nameof(Configurations));

            app.MapGet(
                    "play/configurations",
                    async (ISender sender) =>
                    {
                        var query = new Query();
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .WithTags(nameof(Configurations));
        }
    }
}
