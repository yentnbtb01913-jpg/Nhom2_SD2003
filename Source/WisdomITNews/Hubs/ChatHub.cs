using Microsoft.AspNetCore.SignalR;

namespace WisdomITNews.Hubs;

public class ChatHub : Hub
{
    private static readonly Dictionary<string, HashSet<string>> OnlineUsers = new();

    // Đây là luồng xử lý kết nối chat cộng đồng (báo online)
    // Luồng: 1) Lấy userKey ("user_{id}"/"admin_{id}") từ Session; không có -> thôi
    //        2) Thêm ConnectionId vào danh sách online (nhiều tab/thiết bị -> nhiều connection)
    //        3) Phát "UserOnline" cho tất cả (cập nhật chấm xanh online)
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

    // Đây là luồng xử lý ngắt kết nối chat cộng đồng (báo offline)
    // Luồng: 1) Xóa ConnectionId khỏi mọi user  2) User hết connection -> phát "UserOffline"
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

    // Đây là luồng xử lý vào nhóm chat cộng đồng
    public async Task JoinGroup(int groupId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{groupId}");
    }

    // Đây là luồng xử lý rời nhóm chat cộng đồng
    public async Task LeaveGroup(int groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{groupId}");
    }

    // Đây là luồng xử lý phát tin nhắn nhóm chat cộng đồng
    // (Việc LƯU tin vào DB do ChatController.SendMessage lo; hub chỉ đẩy realtime.)
    public async Task SendToGroup(int groupId, object message)
    {
        await Clients.Group($"chat_{groupId}").SendAsync("ReceiveMessage", message);
    }

    // Đây là luồng xử lý báo "đang gõ" trong nhóm chat
    public async Task Typing(int groupId, string userName)
    {
        await Clients.OthersInGroup($"chat_{groupId}").SendAsync("UserTyping", groupId, userName);
    }

    // Đây là luồng xử lý lấy danh sách người đang online
    public static List<string> GetOnlineUserKeys()
    {
        lock (OnlineUsers) { return OnlineUsers.Keys.ToList(); }
    }

    // Đây là luồng xử lý xác định danh tính người chat cộng đồng
    private static string? GetUserKey(HttpContext ctx)
    {
        var userId = ctx.Session.GetInt32("UserId");
        if (userId.HasValue) return $"user_{userId}";
        var adminId = ctx.Session.GetInt32("AdminId");
        if (adminId.HasValue) return $"admin_{adminId}";
        return null;
    }
}