using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using ZiggyCreatures.Caching.Fusion;

namespace Pwneu.Play.Features.Submissions;

/// <summary>
/// Asynchronously saves submissions.
/// </summary>
public static class SaveSubmissions
{
    public record Command(List<SubmittedEvent> IncorrectSubmissionEvents) : IRequest<Result>;

    internal sealed class Handler(
        ApplicationDbContext context,
        IFusionCache cache,
        IMemberAccess memberAccess)
        : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var memberIds = await memberAccess.GetMemberIdsAsync(cancellationToken);

            var challengeIds = await cache.GetOrSetAsync(Keys.ChallengeIds(), async _ =>
                    await context
                        .Challenges
                        .Select(ch => ch.Id)
                        .ToListAsync(cancellationToken),
                new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(20) },
                cancellationToken);

            // Remove all invalid submissions.
            var validSubmissions = request
                .IncorrectSubmissionEvents
                .Where(se => memberIds.Contains(se.UserId) && challengeIds.Contains(se.ChallengeId))
                .Select(se => new Submission
                {
                    Id = Guid.NewGuid(),
                    UserId = se.UserId,
                    UserName = se.UserName,
                    ChallengeId = se.ChallengeId,
                    Flag = se.Flag,
                    SubmittedAt = se.SubmittedAt,
                    IsCorrect = false
                })
                .ToList();

            context.AddRange(validSubmissions);

            await context.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}

public class SubmittedEventsConsumer(ISender sender, ILogger<SubmittedEventsConsumer> logger)
    : IConsumer<Batch<SubmittedEvent>>
{
    public async Task Consume(ConsumeContext<Batch<SubmittedEvent>> context)
    {
        var incorrectSubmissionEvents = context
            .Message
            .Select(s => s.Message)
            .ToList();

        var command = new SaveSubmissions.Command(incorrectSubmissionEvents);

        var result = await sender.Send(command);

        if (result.IsSuccess)
        {
            logger.LogInformation("Saved submissions to database");
            return;
        }

        logger.LogError("Failed to save submissions to database: {error}", result.Error.Message);
    }
}