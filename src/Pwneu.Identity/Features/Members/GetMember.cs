using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Identity.Shared.Data;
using Pwneu.Identity.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Identity.Features.Members;

/// <summary>
/// Retrieves a user by ID, excluding those with a role of manager or admin.
/// This listener is used for navigating to a user.
/// </summary>
public class GetMember
{
    public record Query(string Id) : IRequest<Result<UserResponse>>;

    private static readonly Error NotFound = new("GetMember.NotFound", "Member not found");

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, IAccessControl accessControl)
        : IRequestHandler<Query, Result<UserResponse>>
    {
        public async Task<Result<UserResponse>> Handle(Query request, CancellationToken cancellationToken)
        {
            var managerIds = await accessControl.GetManagerIdsAsync(cancellationToken);

            if (managerIds.Contains(request.Id))
                return Result.Failure<UserResponse>(NotFound);

            var user = await cache.GetOrSetAsync(Keys.User(request.Id), async _ =>
                await context
                    .Users
                    .Where(u => u.Id == request.Id)
                    .Select(u => new UserResponse
                    {
                        Id = u.Id,
                        UserName = u.UserName
                    })
                    .FirstOrDefaultAsync(cancellationToken), token: cancellationToken);

            return user ?? Result.Failure<UserResponse>(NotFound);
        }
    }

    public class Listener(ISender sender, ILogger<Listener> logger) : IConsumer<MemberRequest>
    {
        public async Task Consume(ConsumeContext<MemberRequest> context)
        {
            var message = context.Message;
            var query = new Query(message.Id);
            var result = await sender.Send(query);

            if (result.IsSuccess)
            {
                logger.LogInformation("Successfully get user: {id}", result.Value);
                await context.RespondAsync(result.Value);
                return;
            }

            logger.LogError("Failed to get user: {message}", result.Error.Message);
            await context.RespondAsync(new UserNotFoundResponse());
        }
    }
}