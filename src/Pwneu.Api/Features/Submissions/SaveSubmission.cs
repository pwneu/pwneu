using MassTransit;
using MediatR;
using Pwneu.Api.Shared.Data;
using Pwneu.Api.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Api.Features.Submissions;

public static class SaveSubmission
{
    public record Command(
        string UserId,
        Guid ChallengeId,
        string Flag,
        DateTime SubmittedAt,
        bool IsCorrect) : IRequest<Result<Guid>>;

    internal sealed class Handler(ApplicationDbContext context)
        : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(Command request, CancellationToken cancellationToken)
        {
            var category = new Submission
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                ChallengeId = request.ChallengeId,
                Flag = request.Flag,
                SubmittedAt = request.SubmittedAt,
                IsCorrect = request.IsCorrect,
            };

            context.Add(category);

            await context.SaveChangesAsync(cancellationToken);

            return category.Id;
        }
    }

    public class Listener(ISender sender, ILogger<Listener> logger) : IConsumer<SubmittedEvent>
    {
        public async Task Consume(ConsumeContext<SubmittedEvent> context)
        {
            var message = context.Message;
            var command = new Command(message.UserId, message.ChallengeId, message.Flag, message.SubmittedAt,
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
}