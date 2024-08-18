using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Users;

public static class EvaluateUser
{
    public record Query(string Id) : IRequest<Result<UserEvalResponse>>;

    private static readonly Error NotFound = new("EvaluateUser.NotFound",
        "The user with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, IMemberAccess memberAccess)
        : IRequestHandler<Query, Result<UserEvalResponse>>
    {
        public async Task<Result<UserEvalResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            // Check if user exists.
            if (!await memberAccess.MemberExistsAsync(request.Id, cancellationToken))
                return Result.Failure<UserEvalResponse>(NotFound);

            // Get all the categories first.
            var categoryIds = await cache.GetOrSetAsync(Keys.CategoryIds(), async _ =>
                await context
                    .Categories
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken), token: cancellationToken);

            var userCategoryEvaluations = new List<UserCategoryEvalResponse>();

            // TODO -- Update cache on user submit
            // TODO -- Update cache on hint usage
            // TODO -- Add user score
            // TODO -- Deduct user score on hint usage
            // TODO -- Create line chart data for submission and hint usage history

            // Cache each user category evaluation.
            foreach (var categoryId in categoryIds)
            {
                var userCategoryEvaluation = await cache.GetOrSetAsync(
                    Keys.UserCategoryEval(request.Id, categoryId),
                    async _ => await context
                        .Categories
                        .Where(c => c.Id == categoryId)
                        .Select(c => new UserCategoryEvalResponse
                        {
                            CategoryId = c.Id,
                            Name = c.Name,
                            TotalChallenges = c.Challenges.Count,
                            TotalSolves = c.Challenges
                                .SelectMany(ch => ch.Submissions)
                                .Count(s => s.UserId == request.Id && s.IsCorrect == true),
                            IncorrectAttempts = c.Challenges
                                .SelectMany(ch => ch.Submissions)
                                .Count(s => s.UserId == request.Id && s.IsCorrect == false),
                            HintsUsed = c.Challenges
                                .SelectMany(ch => ch.Hints)
                                .SelectMany(h => h.HintUsages)
                                .Count(hu => hu.UserId == request.Id)
                        })
                        .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

                if (userCategoryEvaluation is not null)
                    userCategoryEvaluations.Add(userCategoryEvaluation);
            }

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

            return new UserEvalResponse
            {
                Id = request.Id,
                CategoryEvaluations = userCategoryEvaluations
            };
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/{id:Guid}/evaluate", async (Guid id, ISender sender) =>
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