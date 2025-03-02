using FluentValidation;
using MediatR;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Data;
using Pwneu.Api.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Features.Configurations;

public static class SetPublicLeaderboardCount
{
    public record Command(int Count) : IRequest<Result>;

    internal sealed class Handler(
        AppDbContext context,
        IFusionCache cache,
        IValidator<Command> validator,
        ILogger<Handler> logger
    ) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(
                    new Error("SetPublicLeaderboardCount.Validation", validationResult.ToString())
                );

            await context.SetConfigurationValueAsync(
                ConfigurationKeys.PublicLeaderboardCount,
                request.Count,
                cancellationToken
            );

            await cache.RemoveAsync(CacheKeys.PublicLeaderboardCount(), token: cancellationToken);
            await cache.RemoveAsync(CacheKeys.UserRanks(), token: cancellationToken);

            logger.LogInformation(
                "Public leaderboard count has been set to {Count}",
                request.Count
            );

            return Result.Success();
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPut(
                    "play/configurations/publicLeaderboardCount",
                    async (int count, ISender sender) =>
                    {
                        var query = new Command(count);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.NoContent();
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .WithTags(nameof(Configurations));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Count)
                .GreaterThanOrEqualTo(10)
                .WithMessage("The count must be greater than or equal to 10.");
        }
    }
}
