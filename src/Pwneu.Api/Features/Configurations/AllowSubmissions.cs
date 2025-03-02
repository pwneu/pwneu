using MediatR;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions;
using Pwneu.Api.Services;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Configurations;

public static class AllowSubmissions
{
    public record Command : IRequest<Result>;

    internal sealed class Handler(AppDbContext context, IFusionCache cache, ILogger<Handler> logger)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            await context.SetConfigurationValueAsync(
                ConfigurationKeys.SubmissionsAllowed,
                true,
                cancellationToken
            );
            await cache.RemoveAsync(CacheKeys.SubmissionsAllowed(), token: cancellationToken);

            logger.LogInformation("Submissions allowed");

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPut(
                    "play/configurations/submissionsAllowed/allow",
                    async (ISender sender, IChallengePointsConcurrencyGuard guard) =>
                    {
                        if (!await guard.TryEnterAsync())
                            return Results.BadRequest(Error.AnotherProcessRunning);

                        try
                        {
                            var query = new Command();
                            var result = await sender.Send(query);

                            return result.IsFailure
                                ? Results.BadRequest(result.Error)
                                : Results.NoContent();
                        }
                        finally
                        {
                            guard.Exit();
                        }
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .WithTags(nameof(Configurations));
        }
    }
}
