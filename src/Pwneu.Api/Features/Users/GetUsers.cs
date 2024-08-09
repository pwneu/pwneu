using System.Linq.Expressions;
using MediatR;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using Pwneu.Api.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Api.Features.Users;

/// <summary>
/// Retrieves a paginated list of users, excluding those with a role of manager or admin.
/// Only users with manager or admin roles can access this endpoint.
/// </summary>
public static class GetUsers
{
    public record Query(
        string? SearchTerm = null,
        string? SortBy = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null)
        : IRequest<Result<PagedList<UserResponse>>>;

    internal sealed class Handler(ApplicationDbContext context, IAccessControl accessControl)
        : IRequestHandler<Query, Result<PagedList<UserResponse>>>
    {
        public async Task<Result<PagedList<UserResponse>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var managerIds = await accessControl.GetManagerIdsAsync(cancellationToken);

            var usersQuery = context.Users.Where(u => !managerIds.Contains(u.Id));

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                usersQuery = usersQuery.Where(u =>
                    u.UserName != default &&
                    u.UserName.Contains(request.SearchTerm));

            Expression<Func<User, object>> keySelector = request.SortBy?.ToLower() switch
            {
                "username" => user => user.UserName!,
                _ => user => user.CreatedAt
            };

            usersQuery = request.SortOrder?.ToLower() == "desc"
                ? usersQuery.OrderByDescending(keySelector)
                : usersQuery.OrderBy(keySelector);

            var userResponsesQuery = usersQuery.Select(u => new UserResponse
            {
                Id = u.Id,
                UserName = u.UserName
            });

            var users = await PagedList<UserResponse>.CreateAsync(
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
            app.MapGet("users",
                    async (string? searchTerm, string? sortBy, string? sortOrder, int? page, int? pageSize,
                        ISender sender) =>
                    {
                        var query = new Query(searchTerm, sortBy, sortOrder, page, pageSize);
                        var result = await sender.Send(query);

                        return result.IsFailure ? Results.StatusCode(500) : Results.Ok(result.Value);
                    })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Users));
        }
    }
}