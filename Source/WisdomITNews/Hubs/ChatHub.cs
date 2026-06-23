using Microsoft.AspNetCore.SignalR;

namespace WisdomITNews.Hubs;

public class ChatHub : Hub
{
    private static readonly Dictionary<string, HashSet<string>> OnlineUsers = new();

    public override async Task OnConnectedAsync()
    {
        var ctx = Context.GetHttpContext();
        if (ctx == null) { await base.OnConnectedAsync(); return; }

        var userKey = GetUserKey(ctx);
        if (userKey == null) { await base.OnConnectedAsync(); return; }

        lock (OnlineUsers)
        {
            if (!OnlineUsers.ContainsKey(userKey))
                OnlineUsers[userKey] = new HashSet<string>();
            OnlineUsers[userKey].Add(Context.ConnectionId);
        }

        await Clients.All.SendAsync("UserOnline", userKey);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string? offlineUser = null;
        lock (OnlineUsers)
        {
            foreach (var kv in OnlineUsers)
            {
                if (kv.Value.Remove(Context.ConnectionId) && kv.Value.Count == 0)
                {
                    offlineUser = kv.Key;
                    OnlineUsers.Remove(kv.Key);
                    break;
                }
            }
        }

        if (offlineUser != null)
            await Clients.All.SendAsync("UserOffline", offlineUser);

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinGroup(int groupId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{groupId}");
    }

    public async Task LeaveGroup(int groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{groupId}");
    }

    public async Task SendToGroup(int groupId, object message)
    {
        await Clients.Group($"chat_{groupId}").SendAsync("ReceiveMessage", message);
    }

    public async Task Typing(int groupId, string userName)
    {
        await Clients.OthersInGroup($"chat_{groupId}").SendAsync("UserTyping", groupId, userName);
    }

    public static List<string> GetOnlineUserKeys()
    {
        lock (OnlineUsers) { return OnlineUsers.Keys.ToList(); }
    }

    private static string? GetUserKey(HttpContext ctx)
    {
        var userId = ctx.Session.GetInt32("UserId");
        if (userId.HasValue) return $"user_{userId}";
        var adminId = ctx.Session.GetInt32("AdminId");
        if (adminId.HasValue) return $"admin_{adminId}";
        return null;
    }
}