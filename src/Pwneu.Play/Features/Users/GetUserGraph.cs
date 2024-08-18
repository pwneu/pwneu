using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Users;

// TODO -- Cache and invalidate user graph
// TODO -- Fix graph

public static class GetUserGraph
{
    public record Query(string Id) : IRequest<Result<IEnumerable<UserActivityScore>>>;

    private static readonly Error NotFound = new("EvaluateUser.NotFound",
        "The user with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, IMemberAccess memberAccess)
        : IRequestHandler<Query, Result<IEnumerable<UserActivityScore>>>
    {
        public async Task<Result<IEnumerable<UserActivityScore>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            // Check if user exists.
            if (!await memberAccess.MemberExistsAsync(request.Id, cancellationToken))
                return Result.Failure<IEnumerable<UserActivityScore>>(NotFound);

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

            var userGraph = await context.Submissions
                .Where(s => s.UserId == request.Id && s.IsCorrect)
                .Select(s => new UserActivityScore
                {
                    ActivityDate = s.SubmittedAt,
                    Score = s.Challenge.Points - s.Challenge.Hints
                        .Where(h => h.HintUsages.Any(hu => hu.UserId == request.Id))
                        .Sum(h => h.Deduction)
                })
                .Union(context.HintUsages
                    .Where(hu => hu.UserId == request.Id)
                    .Select(hu => new UserActivityScore
                    {
                        ActivityDate = hu.UsedAt,
                        Score = -hu.Hint.Deduction
                    })
                )
                .OrderBy(a => a.ActivityDate)
                .ToListAsync(cancellationToken);

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