using System.Linq.Expressions;
using MediatR;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Identity.Features.Users;

/// <summary>
/// Retrieves a paginated list of users, excluding those with a role of manager or admin.
/// Only users with manager or admin roles can access this endpoint.
/// </summary>
public static class GetUsers
{
    public record Query(
        bool? ExcludeVerified = false,
        string? SearchTerm = null,
        string? SortBy = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null)
        : IRequest<Result<PagedList<UserDetailsResponse>>>;

    internal sealed class Handler(ApplicationDbContext context)
        : IRequestHandler<Query, Result<PagedList<UserDetailsResponse>>>
    {
        public async Task<Result<PagedList<UserDetailsResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            IQueryable<User> usersQuery = context.Users;

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                usersQuery = usersQuery.Where(u =>
                    (u.UserName != null && u.UserName.Contains(request.SearchTerm)) ||
                    (u.Email != null && u.Email.Contains(request.SearchTerm)) ||
                    u.FullName.Contains(request.SearchTerm) ||
                    u.Id.Contains(request.SearchTerm));

            if (request.ExcludeVerified is true)
                usersQuery = usersQuery.Where(u => !u.EmailConfirmed);

            Expression<Func<User, object>> keySelector = request.SortBy?.ToLower() switch
            {
                "username" => user => user.UserName ?? string.Empty,
                "fullname" => user => user.FullName,
                "email" => user => user.Email ?? string.Empty,
                _ => user => user.CreatedAt
            };

            usersQuery = request.SortOrder?.ToLower() == "desc"
                ? usersQuery.OrderByDescending(keySelector)
                : usersQuery.OrderBy(keySelector);

            var userResponsesQuery = usersQuery
                .Select(u => new UserDetailsResponse
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    FullName = u.FullName,
                    CreatedAt = u.CreatedAt,
                    Email = u.Email,
                    EmailConfirmed = u.EmailConfirmed
                });

            var users = await PagedList<UserDetailsResponse>.CreateAsync(
                userResponsesQuery,
                request.Page ?? 1,
                request.PageSize ?? 10);

            return users;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("users", async (bool? excludeVerified, string? searchTerm, string? sortBy, string? sortOrder,
                    int? page, int? pageSize, ISender sender) =>
                {
                    var query = new Query(excludeVerified, searchTerm, sortBy, sortOrder, page, pageSize);
                    var result = await sender.Send(query);

                    return result.IsFailure ? Results.StatusCode(500) : Results.Ok(result.Value);
                })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .RequireRateLimiting(Consts.Fixed)
                .WithTags(nameof(Users));
        }
    }
}