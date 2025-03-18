using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Pwneu.Api.Features.Announcements;

[Authorize]
public sealed class AnnouncementHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> ConnectedUsers = new();

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        var connectionId = Context.ConnectionId;

        if (!string.IsNullOrEmpty(userId))
            ConnectedUsers.TryAdd(connectionId, userId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        ConnectedUsers.TryRemove(Context.ConnectionId, out _);
        await base.OnDisconnectedAsync(exception);
    }

    public static int GetConnectedUserCount()
    {
        return ConnectedUsers.Count;
    }
}
