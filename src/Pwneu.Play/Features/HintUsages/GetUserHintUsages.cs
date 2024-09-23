using System.Linq.Expressions;
using System.Security.Claims;
using MediatR;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;

namespace Pwneu.Play.Features.HintUsages;

public static class GetUserHintUsages
{
    public record Query(
        string Id,
        string? SearchTerm = null,
        string? SortBy = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null)
        : IRequest<Result<PagedList<UserHintUsageResponse>>>;

    private static readonly Error NotFound = new("GetUserHintUsages.NotFound",
        "The user with the specified ID was not found");

    internal sealed class Handler(ApplicationDbContext context, IMemberAccess memberAccess)
        : IRequestHandler<Query, Result<PagedList<UserHintUsageResponse>>>
    {
        public async Task<Result<PagedList<UserHintUsageResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            // Check if user exists.
            if (!await memberAccess.MemberExistsAsync(request.Id, cancellationToken))
                return Result.Failure<PagedList<UserHintUsageResponse>>(NotFound);

            var hintUsagesQuery = context
                .HintUsages
                .Where(hu => hu.UserId == request.Id);

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                hintUsagesQuery = hintUsagesQuery.Where(hu =>
                    hu.Hint.Challenge.Name.Contains(request.SearchTerm));

            Expression<Func<HintUsage, object>> keySelector = request.SortBy?.ToLower() switch
            {
                "name" => hintUsage => hintUsage.Hint.Challenge.Name,
                "challengename" => hintUsage => hintUsage.Hint.Challenge.Name,
                "deduction" => hintUsage => hintUsage.Hint.Deduction,
                _ => hintUsage => hintUsage.UsedAt
            };

            hintUsagesQuery = request.SortOrder?.ToLower() == "desc"
                ? hintUsagesQuery.OrderByDescending(keySelector)
                : hintUsagesQuery.OrderBy(keySelector);

            var hintUsageResponses = hintUsagesQuery
                .Select(hu => new UserHintUsageResponse
                {
                    HintId = hu.HintId,
                    ChallengeId = hu.Hint.ChallengeId,
                    ChallengeName = hu.Hint.Challenge.Name,
                    UsedAt = hu.UsedAt,
                    Deduction = hu.Hint.Deduction,
                });

            var hintUsages = await PagedList<UserHintUsageResponse>.CreateAsync(
                hintUsageResponses,
                request.Page ?? 1,
                Math.Min(request.PageSize ?? 10, 20));

            return hintUsages;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users/{id:Guid}/hintUsages", async (Guid id, string? searchTerm, string? sortBy,
                    string? sortOrder, int? page, int? pageSize, ISender sender) =>
                {
                    var query = new Query(id.ToString(), searchTerm, sortBy, sortOrder, page, pageSize);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization()
                .RequireRateLimiting(Consts.Fixed)
                .WithTags(nameof(HintUsages));

            app.MapGet("me/hintUsages", async (string? searchTerm, string? sortBy, string? sortOrder, int? page,
                    int? pageSize, ClaimsPrincipal claims, ISender sender) =>
                {
                    var id = claims.GetLoggedInUserId<string>();
                    if (id is null) return Results.BadRequest();

                    var query = new Query(id, searchTerm, sortBy, sortOrder, page, pageSize);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.NotFound(result.Error) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.MemberOnly)
                .RequireRateLimiting(Consts.Fixed)
                .WithTags(nameof(HintUsages));
        }
    }
}