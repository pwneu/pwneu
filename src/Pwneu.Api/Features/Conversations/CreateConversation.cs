using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.ChatCompletion;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using Pwneu.Api.Options;
using System.Security.Claims;

namespace Pwneu.Api.Features.Conversations;

public static class CreateConversation
{
    public record Command(string UserId, string Input) : IRequest<Result<string>>;

    private static readonly Error Disabled = new("Chat.Disabled", "Chat is currently disabled");

    private static readonly Error Failed = new("Chat.Failed", "Failed to generate output");

    private static readonly Error LimitReached = new(
        "Chat.LimitReached",
        "Limit has been reached for the user"
    );

    internal sealed class Handler(
        AppDbContext context,
        IChatCompletionService chat,
        IValidator<Command> validator,
        IOptions<ChatOptions> chatOptions,
        ILogger<Handler> logger
    ) : IRequestHandler<Command, Result<string>>
    {
        private readonly ChatOptions _chatOptions = chatOptions.Value;

        public async Task<Result<string>> Handle(
            Command request,
            CancellationToken cancellationToken
        )
        {
            if (!_chatOptions.ConversationIsEnabled)
                return Result.Failure<string>(Disabled);

            var validationResult = await validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
                return Result.Failure<string>(
                    new Error("CreateConversation.Validation", validationResult.ToString())
                );

            var recentConversations = await context
                .Conversations.Where(c =>
                    c.UserId == request.UserId && c.RequestedAt.Date == DateTime.UtcNow.Date
                )
                .CountAsync(cancellationToken);

            var limit = _chatOptions.MaxConversationsPerDay;

            if (recentConversations >= limit)
                return Result.Failure<string>(LimitReached);

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("Dash AI, a CTF chatbot. Answer only cybersecurity questions concisely.");
            chatHistory.AddUserMessage(request.Input);

            string? output;

            try
            {
                var response = await chat.GetChatMessageContentsAsync(
                    chatHistory,
                    cancellationToken: cancellationToken
                );

                output = response[^1].Content;
            }
            catch (Exception ex)
            {
                logger.LogError("Something went wrong creating conversation: {Message}", ex.Message);
                return Result.Failure<string>(Failed);
            }

            if (string.IsNullOrWhiteSpace(output))
                return Result.Failure<string>(Failed);

            var conversation = Conversation.Create(request.UserId, request.Input, output);

            context.Add(conversation);

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Conversation created: {ConversationId}, User: {UserId}",
                conversation.Id,
                request.UserId
            );

            return conversation.Output;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "chat/conversations",
                    async (
                        CreateConversationRequest request,
                        ClaimsPrincipal claims,
                        ISender sender
                    ) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null)
                            return Results.BadRequest();

                        var command = new Command(userId, request.Input);
                        var result = await sender.Send(command);

                        return result.IsFailure
                            ? Results.BadRequest(result.Error)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.MemberOnly)
                .RequireRateLimiting(RateLimitingPolicies.Fixed)
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
