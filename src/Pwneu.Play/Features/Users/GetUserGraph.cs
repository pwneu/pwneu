using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Users;

public static class GetUserGraph
{
    public record Query(string Id) : IRequest<Result<IEnumerable<UserActivityResponse>>>;

    private static readonly Error NotFound = new("GetUserGraph.NotFound",
        "The user with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, IMemberAccess memberAccess)
        : IRequestHandler<Query, Result<IEnumerable<UserActivityResponse>>>
    {
        public async Task<Result<IEnumerable<UserActivityResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            // Check if user exists.
            if (!await memberAccess.MemberExistsAsync(request.Id, cancellationToken))
                return Result.Failure<IEnumerable<UserActivityResponse>>(NotFound);

            var userGraph = await cache.GetOrSetAsync(Keys.UserGraph(request.Id), async _ =>
            {
                // Get the list of correct submissions by the user.
                var correctSubmissions = await context
                    .Submissions
                    .Where(s => s.UserId == request.Id && s.IsCorrect)
                    .Select(s => new UserActivityResponse
                    {
                        UserId = s.UserId,
                        UserName = s.UserName,
                        ActivityDate = s.SubmittedAt,
                        Score = s.Challenge.Points
                    })
                    .ToListAsync(cancellationToken);

                // Get the list of hint usages by the user but store the score in negative form.
                var hintUsages = await context
                    .HintUsages
                    .Where(h => h.UserId == request.Id)
                    .Select(h => new UserActivityResponse
                    {
                        UserId = h.UserId,
                        UserName = h.UserName,
                        ActivityDate = h.UsedAt,
                        Score = -h.Hint.Deduction
                    })
                    .ToListAsync(cancellationToken);

                // Combine correct submissions and hint usages.
                var userGraph = correctSubmissions
                    .Concat(hintUsages)
                    .OrderBy(a => a.ActivityDate)
                    .ToList();

                // Store the cumulative score and update the score of each item in the list.
                var cumulativeScore = 0;
                foreach (var userActivity in userGraph)
                {
                    cumulativeScore += userActivity.Score;
                    userActivity.Score = cumulativeScore;
                }

                return userGraph;
            }, token: cancellationToken);

            var activeUserIds = await cache.GetOrDefaultAsync<List<string>>(
                Keys.ActiveUserIds(),
                token: cancellationToken) ?? [];

            if (!activeUserIds.Contains(request.Id))
                activeUserIds.Add(request.Id);

            // Store the userId to the cache for easier invalidations.
            await cache.SetAsync(
                Keys.ActiveUserIds(),
                activeUserIds, token: cancellationToken);

            return userGraph;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/{id:Guid}/graph", async (Guid id, ISender sender) =>
                {
                    var query = new Query(id.ToString());
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Users));

            app.MapGet("me/graph", async (ClaimsPrincipal claims, ISender sender) =>
                {
                    var id = claims.GetLoggedInUserId<string>();
                    if (id is null) return Results.BadRequest();

                    var query = new Query(id);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .WithTags(nameof(Users));
        }
    }
}