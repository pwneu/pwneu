using Microsoft.EntityFrameworkCore;
using Pwneu.Api.Shared.Data;
using Pwneu.Shared.Common;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Api.Shared.Services;

public class AccessControl(ApplicationDbContext context, IFusionCache cache) : IAccessControl
{
    public async Task<IEnumerable<string>> GetManagerIdsAsync(CancellationToken cancellationToken = default) =>
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