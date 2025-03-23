using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using System.Linq.Expressions;

namespace Pwneu.Api.Features.Users;

public static class GetUsers
{
    public record Query(
        bool? ExcludeVerified = false,
        bool? ExcludeVisibleOnLeaderboards = false,
        bool? ExcludeWithCertificate = false,
        string? SearchTerm = null,
        string? SortBy = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null
    ) : IRequest<Result<PagedList<UserDetailsResponse>>>;

    internal sealed class Handler(AppDbContext context)
        : IRequestHandler<Query, Result<PagedList<UserDetailsResponse>>>
    {
        public async Task<Result<PagedList<UserDetailsResponse>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            IQueryable<User> usersQuery = context.Users;

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                usersQuery = usersQuery.Where(u =>
                    (
                        u.UserName != null
                        && EF.Functions.ILike(u.UserName, $"%{request.SearchTerm}%")
                    )
                    || (u.Email != null && EF.Functions.ILike(u.Email, $"%{request.SearchTerm}%"))
                    || EF.Functions.ILike(u.FullName, $"%{request.SearchTerm}%")
                    || EF.Functions.ILike(u.Id, $"%{request.SearchTerm}%")
                );

            if (request.ExcludeVerified is true)
                usersQuery = usersQuery.Where(u => !u.EmailConfirmed);

            if (request.ExcludeVisibleOnLeaderboards is true)
                usersQuery = usersQuery.Where(u => !u.IsVisibleOnLeaderboards);

            if (request.ExcludeWithCertificate is true)
                usersQuery = usersQuery.Where(u => u.Certificate == null);

            Expression<Func<User, object>> keySelector = request.SortBy?.ToLower() switch
            {
                "username" => user => user.UserName ?? string.Empty,
                "fullname" => user => user.FullName,
                "email" => user => user.Email ?? string.Empty,
                _ => user => user.CreatedAt,
            };

            usersQuery =
                request.SortOrder?.ToLower() == "desc"
                    ? usersQuery.OrderByDescending(keySelector)
                    : usersQuery.OrderBy(keySelector);

            var userResponsesQuery = usersQuery.Select(u => new UserDetailsResponse
            {
                Id = u.Id,
                UserName = u.UserName,
                FullName = u.FullName,
                CreatedAt = u.CreatedAt,
                Email = u.Email,
                EmailConfirmed = u.EmailConfirmed,
                IsVisibleOnLeaderboards = u.IsVisibleOnLeaderboards,
            });

            var users = await PagedList<UserDetailsResponse>.CreateAsync(
                userResponsesQuery,
                request.Page ?? 1,
                Math.Min(request.PageSize ?? 10, 50)
            );

            return users;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "identity/users",
                    async (
                        bool? excludeVerified,
                        bool? excludeVisibleOnLeaderboards,
                        bool? excludeWithCertificate,
                        string? searchTerm,
                        string? sortBy,
                        string? sortOrder,
                        int? page,
                        int? pageSize,
                        ISender sender
                    ) =>
                    {
                        var query = new Query(
                            excludeVerified,
                            excludeVisibleOnLeaderboards,
                            excludeWithCertificate,
                            searchTerm,
                            sortBy,
                            sortOrder,
                            page,
                            pageSize
                        );
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.StatusCode(500)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .RequireRateLimiting(RateLimitingPolicies.GetUsers)
                .WithTags(nameof(Users));
        }
    }
}
