using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Hubs;

// Chat nội bộ gia hạn quảng cáo: group theo từng QC (adchat_{adId}).
// Người tham gia: Nhà báo (chủ QC) và Quản trị (admin/nhân viên).
public class AdChatHub : Hub
{
    private readonly AppDbContext _db;
    public AdChatHub(AppDbContext db) { _db = db; }

    // Xác định người gửi + kiểm quyền với QC này. null = không có quyền.
    private async Task<(string role, int id, string name)?> ResolveSenderAsync(int adId)
    {
        var ctx = Context.GetHttpContext();
        if (ctx == null) return null;
        var ad = await _db.Advertisements.FindAsync(adId);
        if (ad == null) return null;

        // Nhà báo: chỉ được với QC của chính mình
        var jid = ctx.Session.GetInt32("JournalistId");
        if (jid.HasValue)
        {
            if (ad.CreatedByUserId != jid.Value) return null;
            var u = await _db.Users.FindAsync(jid.Value);
            return ("journalist", jid.Value, u?.FullName ?? "Nhà báo");
        }

        // Admin / Nhân viên: được với mọi QC (AdminId lưu dạng string)
        if (int.TryParse(ctx.Session.GetString("AdminId"), out var aid) && aid > 0)
        {
            var role = ctx.Session.GetString("AdminRole") ?? "";
            var name = ctx.Session.GetString("AdminName") ?? "Quản trị";
            var senderRole = (role == "superadmin" || role == "admin") ? "admin" : "nhanvien";
            return (senderRole, aid, name);
        }
        return null;
    }

    public async Task JoinAd(int adId)
    {
        var who = await ResolveSenderAsync(adId);
        if (who == null) return;                       // không quyền -> không vào group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"adchat_{adId}");
    }

    public async Task LeaveAd(int adId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"adchat_{adId}");
    }

    public async Task SendMessage(int adId, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        var who = await ResolveSenderAsync(adId);
        if (who == null) return;

        content = content.Trim();
        if (content.Length > 2000) content = content.Substring(0, 2000);

        bool isJournalist = who.Value.role == "journalist";
        var msg = new AdRenewalMessage
        {
            AdvertisementId    = adId,
            SenderRole         = who.Value.role,
            SenderId           = who.Value.id,
            SenderName         = who.Value.name,
            Content            = content,
            CreatedAt          = DateTime.Now,
            IsReadByJournalist = isJournalist,          // người gửi coi như đã đọc
            IsReadByAdmin      = !isJournalist
        };
        _db.AdRenewalMessages.Add(msg);
        await _db.SaveChangesAsync();

        await Clients.Group($"adchat_{adId}").SendAsync("ReceiveAdMessage", new
        {
            adId,
            id = msg.Id,
            role = msg.SenderRole,
            senderName = msg.SenderName,
            content = msg.Content,
            createdAt = msg.CreatedAt.ToString("HH:mm dd/MM/yyyy"),
            isJournalist
        });
    }
}
