using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Hints;

public class UseHint
{
    public record Command(string UserId, string UserName, Guid HintId) : IRequest<Result<string>>;

    private static readonly Error NotFound = new("UseHint.NotFound",
        "The hint with the specified ID was not found");

    private static readonly Error ChallengeAlreadySolved = new("UseHint.ChallengeAlreadySolved",
        "Challenge already solved");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Command, Result<string>>
    {
        public async Task<Result<string>> Handle(Command request, CancellationToken cancellationToken)
        {
            // Get hint details first.
            var hintDetails = await context
                .Hints
                .Where(h => h.Id == request.HintId)
                .Select(h => new
                {
                    h.Content,
                    h.ChallengeId,
                    h.Challenge.CategoryId,
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (hintDetails is null)
                return Result.Failure<string>(NotFound);

            // Check if the user has already solved the challenge.
            var hasSolved = await cache.GetOrSetAsync(
                Keys.HasSolved(request.UserId, hintDetails.ChallengeId),
                async _ => await context
                    .Solves
                    .AnyAsync(s =>
                        s.UserId == request.UserId &&
                        s.ChallengeId == hintDetails.ChallengeId, cancellationToken), token: cancellationToken);

            if (hasSolved)
                return Result.Failure<string>(ChallengeAlreadySolved);

            // If the user has already used the hint, there's no need to deduct the user's points.
            var alreadyUsedHint = await context
                .HintUsages
                .AnyAsync(hu => hu.UserId == request.UserId &&
                                hu.HintId == request.HintId,
                    cancellationToken);

            // The user can get the hint over and over again.
            if (alreadyUsedHint)
                return hintDetails.Content;

            var hintUsage = new HintUsage
            {
                UserId = request.UserId,
                UserName = request.UserName,
                HintId = request.HintId,
                UsedAt = DateTime.UtcNow
            };

            context.Add(hintUsage);

            await context.SaveChangesAsync(cancellationToken);

            var invalidationTasks = new List<Task>
            {
                cache.RemoveAsync(Keys.UserGraph(request.UserId), token: cancellationToken).AsTask(),
                cache.RemoveAsync(
                        Keys.UserCategoryEval(request.UserId, hintDetails.CategoryId),
                        token: cancellationToken)
                    .AsTask(),
                cache.RemoveAsync(Keys.UserRanks(), token: cancellationToken).AsTask(),
                cache.RemoveAsync(Keys.TopUsersGraph(), token: cancellationToken).AsTask(),
            };

            await Task.WhenAll(invalidationTasks);

            return hintDetails.Content;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("hints/{id:Guid}", async (Guid id, ClaimsPrincipal claims, ISender sender) =>
                {
                    var userId = claims.GetLoggedInUserId<string>();
                    if (userId is null) return Results.BadRequest();

                    var userName = claims.GetLoggedInUserName();
                    if (userName is null) return Results.BadRequest();

                    var query = new Command(userId, userName, id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.MemberOnly)
                .RequireRateLimiting(Consts.Fixed)
                .WithTags(nameof(Hints));
        }
    }
}