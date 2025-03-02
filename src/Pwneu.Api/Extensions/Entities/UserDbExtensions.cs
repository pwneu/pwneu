using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Extensions.Entities;

public static class UserDbExtensions
{
    public static async Task<bool> CheckIfUserExistsAsync(
        this IFusionCache cache,
        AppDbContext context,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrSetAsync(
            CacheKeys.UserExists(userId),
            async _ => await context.CheckIfUserExistsAsync(userId, cancellationToken),
            new FusionCacheEntryOptions { Duration = TimeSpan.FromHours(1) },
            cancellationToken
        );
    }

    public static async Task<bool> CheckIfUserExistsAsync(
        this AppDbContext context,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        return await context.Users.AnyAsync(u => u.Id == userId, cancellationToken);
    }

    public static async Task<UserDetailsNoEmailResponse?> GetUserDetailsNoEmailAsync(
        this IFusionCache cache,
        AppDbContext context,
        UserManager<User> userManager,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrSetAsync(
            CacheKeys.UserDetailsNoEmail(userId),
            async _ => await context.GetUserDetailsNoEmailAsync(userManager, userId, cancellationToken),
            new FusionCacheEntryOptions { Duration = TimeSpan.FromHours(1) },
            cancellationToken
        );
    }

    public static async Task<UserDetailsNoEmailResponse?> GetUserDetailsNoEmailAsync(
        this AppDbContext context,
        UserManager<User> userManager,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var user = await context
            .Users.Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return null;

        var roles = await userManager.GetRolesAsync(user);

        var userDetails = new UserDetailsNoEmailResponse
        {
            Id = user.Id,
            UserName = user.UserName,
            FullName = user.FullName,
            CreatedAt = user.CreatedAt,
            EmailConfirmed = user.EmailConfirmed,
            IsVisibleOnLeaderboards = user.IsVisibleOnLeaderboards,
            Roles = [.. roles],
        };

        return userDetails;
    }

    public static async Task<List<UserCategoryEvaluationResponse>> GetUserEvaluationsAsync(
        this IFusionCache cache,
        AppDbContext context,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        return await cache.GetOrSetAsync(
            CacheKeys.UserCategoryEvaluations(userId),
            async _ => await context.GetUserEvaluationsAsync(userId, cancellationToken),
            token: cancellationToken
        );
    }

    public static async Task<List<UserCategoryEvaluationResponse>> GetUserEvaluationsAsync(
        this AppDbContext context,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Categories.Select(category => new UserCategoryEvaluationResponse
            {
                CategoryId = category.Id,
                Name = category.Name,
                TotalChallenges = category.Challenges.Count,
                TotalSolves = category
                    .Challenges.SelectMany(c => c.Solves)
                    .Count(s => s.UserId == userId),
                IncorrectAttempts = category
                    .Challenges.SelectMany(c => c.Submissions)
                    .Count(sub => sub.UserId == userId),
                HintsUsed = category
                    .Challenges.SelectMany(c => c.Hints)
                    .SelectMany(h => h.HintUsages)
                    .Count(hu => hu.UserId == userId),
            })
            .ToListAsync(cancellationToken);
    }
}
