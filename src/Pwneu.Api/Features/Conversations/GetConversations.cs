using MediatR;
using Pwneu.Api.Common;
using Pwneu.Api.Constants;
using Pwneu.Api.Contracts;
using Pwneu.Api.Data;
using Pwneu.Api.Entities;
using Pwneu.Api.Extensions;
using System.Security.Claims;

namespace Pwneu.Api.Features.Conversations;

public static class GetConversations
{
    public record Query(
        string? UserId = null,
        string? SortOrder = null,
        int? Page = null,
        int? PageSize = null
    ) : IRequest<Result<PagedList<ConversationResponse>>>;

    internal sealed class Handler(AppDbContext context)
        : IRequestHandler<Query, Result<PagedList<ConversationResponse>>>
    {
        public async Task<Result<PagedList<ConversationResponse>>> Handle(
            Query request,
            CancellationToken cancellationToken
        )
        {
            IQueryable<Conversation> conversationsQuery = context.Conversations;

            if (!string.IsNullOrWhiteSpace(request.UserId))
                conversationsQuery = conversationsQuery.Where(c => c.UserId == request.UserId);

            conversationsQuery =
                request.SortOrder?.ToLower() == "desc"
                    ? conversationsQuery.OrderByDescending(c => c.RequestedAt)
                    : conversationsQuery.OrderBy(c => c.RequestedAt);

            var conversationResponseQuery = conversationsQuery.Select(c => new ConversationResponse
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
                Math.Min(request.PageSize ?? 10, 20)
            );

            return conversations;
        }
    }

    public class Endpoint : IV1Endpoint
    {
        public void MapV1Endpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "chat/conversations",
                    async (
                        string? userId,
                        string? sortOrder,
                        int? page,
                        int? pageSize,
                        ISender sender
                    ) =>
                    {
                        var query = new Query(userId, sortOrder, page, pageSize);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.StatusCode(500)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.ManagerAdminOnly)
                .RequireRateLimiting(RateLimitingPolicies.Fixed)
                .WithTags(nameof(Conversations));

            app.MapGet(
                    "chat/conversations/me",
                    async (
                        ClaimsPrincipal claims,
                        string? sortOrder,
                        int? page,
                        int? pageSize,
                        ISender sender
                    ) =>
                    {
                        var userId = claims.GetLoggedInUserId<string>();
                        if (userId is null)
                            return Results.BadRequest();

                        var query = new Query(userId, sortOrder, page, pageSize);
                        var result = await sender.Send(query);

                        return result.IsFailure
                            ? Results.StatusCode(500)
                            : Results.Ok(result.Value);
                    }
                )
                .RequireAuthorization(AuthorizationPolicies.MemberOnly)
                .RequireRateLimiting(RateLimitingPolicies.Fixed)
                .WithTags(nameof(Conversations));
        }
    }
}
