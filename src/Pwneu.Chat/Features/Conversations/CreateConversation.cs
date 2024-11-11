using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;
using Pwneu.Chat.Shared.Data;
using Pwneu.Chat.Shared.Entities;
using Pwneu.Chat.Shared.Options;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;

namespace Pwneu.Chat.Features.Conversations;

public static class CreateConversation
{
    public record Command(string UserId, string Input) : IRequest<Result<string>>;

    private static readonly Error Disabled = new("Chat.Disabled",
        "Chat is currently disabled");

    private static readonly Error Failed = new("Chat.Failed",
        "Failed to generate output");

    private static readonly Error LimitReached = new("Chat.LimitReached",
        "Limit has been reached for the user");

    internal sealed class Handler(
        ApplicationDbContext context,
        IChatCompletionService chat,
        IValidator<Command> validator,
        IOptions<ChatOptions> chatOptions,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result<string>>
    {
        private readonly ChatOptions _chatOptions = chatOptions.Value;

        public async Task<Result<string>> Handle(Command request, CancellationToken cancellationToken)
        {
            if (!_chatOptions.ConversationIsEnabled)
                return Result.Failure<string>(Disabled);

            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<string>(new Error("Chat.Validation", validationResult.ToString()));

            var recentConversations = await context
                .Conversations
                .Where(c => c.UserId == request.UserId &&
                            c.RequestedAt.Date == DateTime.UtcNow.Date)
                .CountAsync(cancellationToken);

            var limit = _chatOptions.MaxConversationsPerDay;

            if (recentConversations >= limit)
                return Result.Failure<string>(LimitReached);

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("Only accept questions about cybersecurity.");
            chatHistory.AddUserMessage(request.Input);

            string? output;

            try
            {
                var response = await chat.GetChatMessageContentsAsync(
                    chatHistory,
                    cancellationToken: cancellationToken);

                output = response[^1].Content;
            }
            catch (Exception)
            {
                return Result.Failure<string>(Failed);
            }

            if (string.IsNullOrWhiteSpace(output))
                return Result.Failure<string>(Failed);

            var conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Input = request.Input,
                Output = output,
                RequestedAt = DateTime.UtcNow
            };

            context.Add(conversation);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Conversation created: {ConversationId}, User: {UserId}",
                conversation.Id,
                request.UserId);

            return conversation.Output;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("/conversations",
                    async (CreateConversationRequest request, ClaimsPrincipal claims, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null) return Results.BadRequest();

                        var command = new Command(userId, request.Input);
                        var result = await sender.Send(command);

                        return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
                    })
                .RequireAuthorization(Consts.MemberOnly)
                .RequireRateLimiting(Consts.Fixed)
                .WithTags(nameof(Conversations));
        }
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.UserId)
                .NotEmpty()
                .WithMessage("User Id is required.")
                .MaximumLength(36)
                .WithMessage("User Id must be 36 characters.");

            RuleFor(c => c.Input)
                .NotEmpty()
                .WithMessage("Input is required.")
                .MaximumLength(100)
                .WithMessage("Input must be 100 characters or less.");
        }
    }
}