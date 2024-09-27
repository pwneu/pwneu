using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pwneu.Chat.Shared.Data;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;

namespace Pwneu.Chat.Features.Conversations;

// TODO -- Test

public static class ClearUserConversations
{
    public record Command(string UserId) : IRequest<Result>;

    internal sealed class Handler(ApplicationDbContext context) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            await context
                .Conversations
                .Where(hu => hu.UserId == request.UserId)
                .ExecuteDeleteAsync(cancellationToken);

            return Result.Success();
        }
    }
}

public class ChatUserDeletedEventConsumer(ISender sender, ILogger<ChatUserDeletedEventConsumer> logger)
    : IConsumer<UserDeletedEvent>
{
    public async Task Consume(ConsumeContext<UserDeletedEvent> context)
    {
        try
        {
            logger.LogInformation("Received user deleted event message");

            var message = context.Message;
            var command = new ClearUserConversations.Command(message.Id);
            var result = await sender.Send(command);

            if (result.IsSuccess)
            {
                logger.LogInformation("Successfully delete user conversations: {userId}", context.Message.Id);
                return;
            }

            logger.LogError("Failed to delete user conversations: {userId}", context.Message.Id);
        }
        catch (Exception e)
        {
            logger.LogError("Failed to delete user conversations: {e}", e.Message);
        }
    }
}