using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
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

            // Get user graph in the cache.
            var userGraph = await cache.GetOrDefaultAsync<List<UserActivityResponse>>(
                Keys.UserGraph(request.Id),
                token: cancellationToken);

            // If there's a cache hit, return it immediately.
            if (userGraph is not null)
                return userGraph.ToList();

            // Get the list of correct submissions by the user.
            var correctSubmissions = await context
                .Submissions
                .Where(s => s.UserId == request.Id & s.IsCorrect)
                .Select(s => new UserActivityResponse
                {
                    UserId = s.UserId,
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
                    ActivityDate = h.UsedAt,
                    Score = -h.Hint.Deduction
                })
                .ToListAsync(cancellationToken);

            // Combine correct submissions and hint usages.
            userGraph = correctSubmissions
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

            await cache.SetAsync(Keys.UserGraph(request.Id), userGraph, token: cancellationToken);

            var activeUserIds = await cache.GetOrDefaultAsync<List<string>>(
                Keys.ActiveUserIds(),
                token: cancellationToken) ?? [];

            // TODO -- use HashSet if possible
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
        }
    }
}