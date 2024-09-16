using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.Users;

public static class GetMembers
{
    public record Query : IRequest<Result<IEnumerable<UserResponse>>>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, IAccessControl accessControl)
        : IRequestHandler<Query, Result<IEnumerable<UserResponse>>>
    {
        public async Task<Result<IEnumerable<UserResponse>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var managerIds = await accessControl.GetManagerIdsAsync(cancellationToken);

            var members = await cache.GetOrSetAsync(Keys.Members(), async _ =>
                await context
                    .Users
                    .Where(u => !managerIds.Contains(u.Id))
                    .Select(u => new UserResponse { Id = u.Id, UserName = u.UserName })
                    .ToListAsync(cancellationToken), token: cancellationToken);

            return members;
        }
    }
}

public class GetMembersConsumer(ISender sender, ILogger<GetMembersConsumer> logger)
    : IConsumer<GetMembersRequest>
{
    public async Task Consume(ConsumeContext<GetMembersRequest> context)
    {
        try
        {
            var query = new GetMembers.Query();
            var result = await sender.Send(query);

            if (result.IsSuccess)
            {
                logger.LogInformation("Successfully get members");
                await context.RespondAsync(new UserResponses
                {
                    Users = result.Value
                });
                return;
            }

            logger.LogError("Failed to get members: {message}", result.Error.Message);
            await context.RespondAsync(new UserResponses());
        }
        catch (Exception e)
        {
            logger.LogError("{e}", e.Message);
        }
    }
}