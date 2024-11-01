using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.Users;

public static class GetMemberIds
{
    public record Query : IRequest<Result<MemberIdsResponse>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache)
        : IRequestHandler<Query, Result<MemberIdsResponse>>
    {
        public async Task<Result<MemberIdsResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var memberIds = await cache.GetOrSetAsync(Keys.MemberIds(), async _ =>
                    await context.Users
                        .Where(user => !context.UserRoles
                            .Where(ur => ur.RoleId == context.Roles
                                .Where(role => role.Name == Consts.Manager)
                                .Select(role => role.Id)
                                .FirstOrDefault())
                            .Select(ur => ur.UserId)
                            .Contains(user.Id))
                        .Select(user => user.Id)
                        .ToListAsync(cancellationToken),
                new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(20) },
                cancellationToken);

            return new MemberIdsResponse
            {
                MemberIds = memberIds
            };
        }
    }
}

public class GetMemberIdsConsumer(ISender sender, ILogger<GetMemberIdsConsumer> logger)
    : IConsumer<GetMemberIdsRequest>
{
    public async Task Consume(ConsumeContext<GetMemberIdsRequest> context)
    {
        try
        {
            var query = new GetMemberIds.Query();
            var result = await sender.Send(query);

            if (result.IsSuccess)
            {
                logger.LogInformation("Successfully get member ids");
                await context.RespondAsync(result.Value);
                return;
            }

            logger.LogError("Failed to get member ids: {message}", result.Error.Message);
            await context.RespondAsync(new List<string>());
        }
        catch (Exception e)
        {
            logger.LogError("{e}", e.Message);
        }
    }
}