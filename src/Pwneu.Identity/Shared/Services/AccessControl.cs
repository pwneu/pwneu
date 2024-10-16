using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Data;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Shared.Services;

public class AccessControl(ApplicationDbContext context, IFusionCache cache) : IAccessControl
{
    public async Task<IEnumerable<string>> GetManagerIdsAsync(CancellationToken cancellationToken = default) =>
        // No need to invalidate cache when deleting managers since Guids are always unique.
        await cache.GetOrSetAsync("managerIds", async _ =>
            await context
                .UserRoles
                .Where(ur => context
                    .Roles
                    .Where(r =>
                        r.Name != null &&
                        (r.Name.Equals(Consts.Manager) ||
                         r.Name.Equals(Consts.Admin)))
                    .Select(r => r.Id)
                    .Contains(ur.RoleId))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync(cancellationToken), token: cancellationToken);
}

public interface IAccessControl
{
    Task<IEnumerable<string>> GetManagerIdsAsync(CancellationToken cancellationToken = default);
}