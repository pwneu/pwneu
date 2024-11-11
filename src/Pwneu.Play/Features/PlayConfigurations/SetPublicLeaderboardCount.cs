using FluentValidation;
using MediatR;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Extensions;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.PlayConfigurations;

public static class SetPublicLeaderboardCount
{
    public record Command(int Count) : IRequest<Result>;

    internal sealed class Handler(
        ApplicationDbContext context,
        IFusionCache cache,
        IValidator<Command> validator,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<Guid>(new Error(
                    "SetPublicLeaderboardCount.Validation",
                    validationResult.ToString()));

            await context.SetPlayConfigurationValueAsync(
                Consts.PublicLeaderboardCount,
                request.Count,
                cancellationToken);

            await cache.RemoveAsync(Keys.PublicLeaderboardCount(), token: cancellationToken);

            logger.LogInformation("Public leaderboard count has been set to {Count}", request.Count);

            return Result.Success();
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPut("configurations/publicLeaderboardCount", async (int count, ISender sender) =>
                {
                    var query = new Command(count);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.NoContent();
                })
                .RequireAuthorization(Consts.AdminOnly)
                .WithTags(nameof(PlayConfigurations));
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