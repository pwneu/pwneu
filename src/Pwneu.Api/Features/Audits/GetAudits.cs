using MediatR;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using System.Linq.Expressions;

namespace Pwneu.Api.Features.Audits;

public static class GetAudits
{
    public record Query(
        string? SearchTerm = null,
        string? SortBy = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null
    ) : IRequest<Result<PagedList<AuditResponse>>>;

    internal sealed class Handler(AppDbContext context)
        : IRequestHandler<Query, Result<PagedList<AuditResponse>>>
    {
        public async Task<Result<PagedList<AuditResponse>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            IQueryable<Audit> auditsQuery = context.Audits;

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                auditsQuery = auditsQuery.Where(au =>
                    au.Action.Contains(request.SearchTerm)
                    || au.UserName.Contains(request.SearchTerm)
                    || au.UserId.Contains(request.SearchTerm)
                    || au.Id.ToString().Contains(request.SearchTerm)
                );

            Expression<Func<Audit, object>> keySelector = request.SortBy?.ToLower() switch
            {
                "username" => audit => audit.UserName,
                _ => audit => audit.PerformedAt,
            };

            auditsQuery =
                request.SortOrder?.ToLower() == "desc"
                    ? auditsQuery.OrderByDescending(keySelector)
                    : auditsQuery.OrderBy(keySelector);

            var auditResponsesQuery = auditsQuery.Select(au => new AuditResponse
            {
                Id = au.Id,
                UserId = au.UserId,
                UserName = au.UserName,
                Action = au.Action,
                PerformedAt = au.PerformedAt,
            });

            var audits = await PagedList<AuditResponse>.CreateAsync(
                auditResponsesQuery,
                request.Page ?? 1,
                Math.Min(request.PageSize ?? 10, 50)
            );

            return audits;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "play/audits",
                    async (
                        string? searchTerm,
                        string? sortBy,
                        string? sortOrder,
                        int? page,
                        int? pageSize,
                        ISender sender
                    ) =>
                    {
                        var query = new Query(
                            SearchTerm: searchTerm,
                            SortBy: sortBy,
                            SortOrder: sortOrder,
                            Page: page,
                            PageSize: pageSize
                        );
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.StatusCode(500)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.AdminOnly)
                .RequireRateLimiting(RateLimitingPolicies.Fixed)
                .WithTags(nameof(Audits));
        }
    }
}
