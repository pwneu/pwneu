using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Api.Features.Members;

public static class GetMemberStats
{
    public record Query(string Id) : IRequest<Result<MemberStatsResponse>>;

    private static readonly Error NotFound = new("GetMemberStats.NotFound", "Member not found");

    internal sealed class Handler(ApplicationDbContext context, IMemberAccess memberAccess)
        : IRequestHandler<Query, Result<MemberStatsResponse>>
    {
        public async Task<Result<MemberStatsResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            if (!await memberAccess.MemberExistsAsync(request.Id, cancellationToken))
                return Result.Failure<MemberStatsResponse>(NotFound);

            // TODO -- Cache member stats
            var userStats = //await cache.GetOrSetAsync(Keys.UserStats(request.Id), async _ =>
                new MemberStatsResponse
                {
                    Id = request.Id,
                    Evaluations = await context
                        .Categories
                        .Select(c => new CategoryEvalResponse
                        {
                            Id = c.Id,
                            Name = c.Name,
                            TotalChallenges = c.Challenges.Count,
                            TotalSolves = c.Challenges
                                .SelectMany(ch => ch.Submissions)
                                .Count(s => s.UserId == request.Id && s.IsCorrect == true),
                            IncorrectAttempts = c.Challenges
                                .SelectMany(ch => ch.Submissions)
                                .Count(s => s.UserId == request.Id && s.IsCorrect == false)
                        })
                        .ToListAsync(cancellationToken)
                };
            // , token: cancellationToken);

            return userStats;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("members/{id:Guid}/stats/",
                    async (Guid id, ISender sender) =>
                    {
                        var query = new Query(id.ToString());
                        var result = await sender.Send(query);

                        return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                    })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Members));
        }
    }
}