using System.Security.Claims;
using MediatR;
using Pwneu.Chat.Shared.Data;
using Pwneu.Chat.Shared.Entities;
using Pwneu.Shared.Common;
using Pwneu.Shared.Contracts;
using Pwneu.Shared.Extensions;

namespace Pwneu.Chat.Features.Conversations;

// TODO -- Test

public static class GetConversations
{
    public record Query(
        string? UserId = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null) : IRequest<Result<PagedList<ConversationResponse>>>;

    internal sealed class Handler(ApplicationDbContext context)
        : IRequestHandler<Query, Result<PagedList<ConversationResponse>>>
    {
        public async Task<Result<PagedList<ConversationResponse>>> Handle(Query request,
            CancellationToken cancellationToken)
        {
            IQueryable<Conversation> conversationsQuery = context.Conversations;

            if (!string.IsNullOrWhiteSpace(request.UserId))
                conversationsQuery = conversationsQuery.Where(c => c.UserId == request.UserId);

            conversationsQuery = request.SortOrder?.ToLower() == "desc"
                ? conversationsQuery.OrderByDescending(c => c.RequestedAt)
                : conversationsQuery.OrderBy(c => c.RequestedAt);

            var conversationResponseQuery = conversationsQuery
                .Select(c => new ConversationResponse
                {
                    Id = c.Id,
                    UserId = c.UserId,
                    Input = c.Input,
                    Output = c.Output,
                    RequestedAt = c.RequestedAt,
                });

            var conversations = await PagedList<ConversationResponse>.CreateAsync(
                conversationResponseQuery,
                request.Page ?? 1,
                Math.Min(request.PageSize ?? 10, 20));

            return conversations;
        }
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("conversations",
                    async (string? userId, string? sortOrder, int? page, int? pageSize, ISender sender) =>
                    {
                        var query = new Query(userId, sortOrder, page, pageSize);
                        var result = await sender.Send(query);

                        return result.IsFailure ? Results.StatusCode(500) : Results.Ok(result.Value);
                    })
                .RequireAuthorization(Consts.ManagerAdminOnly)
                .WithTags(nameof(Conversations));

            app.MapGet("conversations/me",
                    async (ClaimsPrincipal claims, string? sortOrder, int? page, int? pageSize, ISender sender) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null) return Results.BadRequest();

                        var query = new Query(userId, sortOrder, page, pageSize);
                        var result = await sender.Send(query);

                        return result.IsFailure ? Results.StatusCode(500) : Results.Ok(result.Value);
                    })
                .RequireAuthorization(Consts.MemberOnly)
                .RequireRateLimiting(Consts.Fixed)
                .WithTags(nameof(Conversations));
        }
    }
}