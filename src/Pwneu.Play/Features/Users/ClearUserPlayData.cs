using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Users;

public static class ClearUserPlayData
{
    public record Command(string UserId) : IRequest<Result>;

    internal sealed class Handler(ApplicationDbContext context, IFusionCache cache, ILogger<Handler> logger)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var userSolvedChallengeIds = await context
                .Solves
                .Where(s => s.UserId == request.UserId)
                .Select(s => s.ChallengeId)
                .Distinct()
                .ToListAsync(cancellationToken);

            await context
                .Challenges
                .Where(ch => userSolvedChallengeIds.Contains(ch.Id))
                .ExecuteUpdateAsync(s =>
                        s.SetProperty(ch => ch.SolveCount, ch => ch.SolveCount - 1),
                    cancellationToken);

            await context
                .Submissions
                .Where(s => s.UserId == request.UserId)
                .ExecuteDeleteAsync(cancellationToken);

            await context
                .Solves
                .Where(s => s.UserId == request.UserId)
                .ExecuteDeleteAsync(cancellationToken);

            await context
                .HintUsages
                .Where(hu => hu.UserId == request.UserId)
                .ExecuteDeleteAsync(cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            var categoryIds = await context
                .Categories
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            var invalidationTasks = new List<Task>
            {
                cache.RemoveAsync(Keys.UserGraph(request.UserId), token: cancellationToken).AsTask(),
                cache.RemoveAsync(Keys.UserSolveIds(request.UserId), token: cancellationToken).AsTask(),
                cache.RemoveAsync(Keys.UserRanks(), token: cancellationToken).AsTask(),
                cache.RemoveAsync(Keys.TopUsersGraph(), token: cancellationToken).AsTask(),
            };

            invalidationTasks.AddRange(userSolvedChallengeIds
                .Select(userSolvedChallengeId =>
                    cache.RemoveAsync(
                            Keys.ChallengeDetails(userSolvedChallengeId),
                            token: cancellationToken)
                        .AsTask()));

            invalidationTasks.AddRange(categoryIds
                .Select(categoryId =>
                    cache.RemoveAsync(
                            Keys.UserCategoryEval(request.UserId, categoryId),
                            token: cancellationToken)
                        .AsTask()));

            await Task.WhenAll(invalidationTasks);

            logger.LogInformation("User play data removed: {UserId}", request.UserId);

            return Result.Success();
        }
    }
}

public class UserDeletedEventConsumer(ISender sender, ILogger<UserDeletedEventConsumer> logger)
    : IConsumer<UserDeletedEvent>
{
    public async Task Consume(ConsumeContext<UserDeletedEvent> context)
    {
        try
        {
            logger.LogInformation("Received user deleted event message");

            var message = context.Message;
            var command = new ClearUserPlayData.Command(message.Id);
            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                logger.LogInformation("Successfully deleted user play data: {userId}", context.Message.Id);
                return;
            }

            logger.LogError("Failed to delete user submissions: {userId}", context.Message.Id);
        }
        catch (Exception e)
        {
            logger.LogError("{e}", e.Message);
        }
    }
}