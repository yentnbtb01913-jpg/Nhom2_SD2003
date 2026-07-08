using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Hubs;

// Chat nội bộ ban quản lý: phòng chung ("team") + nhắn riêng (DM). Group SignalR = tc_{convKey}.
public class TeamChatHub : Hub
{
    private readonly AppDbContext _db;
    public TeamChatHub(AppDbContext db) { _db = db; }

    // Chỉ Admin/Nhân viên (AdminId) và Nhà báo (JournalistId) mới có danh tính.
    private async Task<(string role, int id, string name)?> ResolveMeAsync()
    {
        var ctx = Context.GetHttpContext();
        if (ctx == null) return null;

        var jid = ctx.Session.GetInt32("JournalistId");
        if (jid.HasValue)
        {
            var u = await _db.Users.FindAsync(jid.Value);
            return ("journalist", jid.Value, u?.FullName ?? "Nhà báo");
        }
        if (int.TryParse(ctx.Session.GetString("AdminId"), out var aid) && aid > 0)
        {
            var role = ctx.Session.GetString("AdminRole") ?? "";
            var name = ctx.Session.GetString("AdminName") ?? "Quản trị";
            return ((role == "superadmin" || role == "admin") ? "admin" : "nhanvien", aid, name);
        }
        return null;
    }

    public async Task JoinConversation(string convKey)
    {
        if (string.IsNullOrWhiteSpace(convKey)) return;
        var me = await ResolveMeAsync();
        if (me == null) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tc_{convKey}");
    }

    public async Task LeaveConversation(string convKey)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tc_{convKey}");
    }

    public async Task SendMessage(string convKey, string content, string? recipientRole, int? recipientId)
    {
        if (string.IsNullOrWhiteSpace(convKey) || string.IsNullOrWhiteSpace(content)) return;
        var me = await ResolveMeAsync();
        if (me == null) return;

        content = content.Trim();
        if (content.Length > 2000) content = content.Substring(0, 2000);

        bool isGroup = convKey == TeamChatKeys.Group;
        var msg = new TeamChatMessage
        {
            ConversationKey = convKey,
            IsGroup = isGroup,
            SenderRole = me.Value.role,
            SenderId = me.Value.id,
            SenderName = me.Value.name,
            RecipientRole = isGroup ? null : recipientRole,
            RecipientId = isGroup ? null : recipientId,
            Content = content,
            CreatedAt = DateTime.Now
        };
        _db.TeamChatMessages.Add(msg);
        await _db.SaveChangesAsync();

        await Clients.Group($"tc_{convKey}").SendAsync("ReceiveTeamMessage", new
        {
            convKey,
            id = msg.Id,
            role = msg.SenderRole,
            senderId = msg.SenderId,
            senderName = msg.SenderName,
            content = msg.Content,
            createdAt = msg.CreatedAt.ToString("HH:mm dd/MM/yyyy")
        });
    }
}
