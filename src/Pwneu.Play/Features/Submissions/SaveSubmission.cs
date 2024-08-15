using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Play.Shared.Data;
using Pwneu.Play.Shared.Entities;
using Pwneu.Play.Shared.Services;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Play.Features.Submissions;

public static class SaveSubmission
{
    public record Command(
        string UserId,
        Guid ChallengeId,
        string Flag,
        DateTime SubmittedAt,
        bool IsCorrect) : IRequest<Result<Guid>>;

    private static readonly Error UserNotFound = new("SaveSubmission.UserNotFound",
        "The user with the specified ID was not found");

    private static readonly Error AlreadySolved = new("SaveSubmission.AlreadySolved",
        "The user already solved the challenge");

    internal sealed class Handler(
        ApplicationDbContext context,
        IMemberAccess memberAccess)
        : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
        {
            // Check if user exists.
            if (!await memberAccess.MemberExistsAsync(request.UserId, cancellationToken))
                return Result.Failure<Guid>(UserNotFound);

            // TODO -- Check if challenge exists

            if (request.IsCorrect)
            {
                var alreadySolved = await context
                    .Submissions
                    .AnyAsync(s =>
                        s.UserId == request.UserId &&
                        s.ChallengeId == request.ChallengeId &&
                        s.IsCorrect, cancellationToken);

                // Prevent double correct submissions.
                // We're checking this because just in case the message queue
                // was late enough that the "already solved" cache is gone.
                if (alreadySolved)
                    return Result.Failure<Guid>(AlreadySolved);
            }

            // Create a new submission and store it to the database.
            var submission = new Submission
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                ChallengeId = request.ChallengeId,
                Flag = request.Flag,
                SubmittedAt = request.SubmittedAt,
                IsCorrect = request.IsCorrect,
            };

            context.Add(submission);

            await context.SaveChangesAsync(cancellationToken);

            // TODO -- Update user evaluation in category

            return submission.Id;
        }
    }
}

public class SubmittedEventConsumer(ISender sender, ILogger<SubmittedEventConsumer> logger) : IConsumer<SubmittedEvent>
{
    public async Task Consume(ConsumeContext<SubmittedEvent> context)
    {
        var message = context.Message;
        var command = new SaveSubmission.Command(message.UserId, message.ChallengeId, message.Flag, message.SubmittedAt,
            message.IsCorrect);

        var result = await sender.Send(command);

        if (result.IsSuccess)
        {
            logger.LogInformation("Saved submission to database: {id}", result.Value);
            return;
        }

        logger.LogError("Failed to save submission to database");
    }
}