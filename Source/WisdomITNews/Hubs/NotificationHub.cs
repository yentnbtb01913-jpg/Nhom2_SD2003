using Microsoft.AspNetCore.SignalR;
using WisdomITNews.Data;

namespace WisdomITNews.Hubs;

public class NotificationHub : Hub
{
    private readonly AppDbContext _db;

    public NotificationHub(AppDbContext db)
    {
        _db = db;
    }

    // Đây là luồng xử lý kết nối nhận thông báo realtime
    // Luồng: 1) Đọc UserId/AdminId/UserEmail/UserRole từ Session
    //        2) Vào group "user_{id}" (thông báo cá nhân), "email_{email}", "journalists" (nếu là Nhà báo)
    //        3) Luôn vào group "all" (thông báo hệ thống cho mọi người)
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var userId = httpContext?.Session.GetInt32("UserId");
        var adminId = httpContext?.Session.GetString("AdminId");
        var userEmail = httpContext?.Session.GetString("UserEmail");
        var role = httpContext?.Session.GetString("UserRole");

        if (userId.HasValue)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

        if (!string.IsNullOrEmpty(userEmail))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"email_{userEmail}");

        if (role == "Journalist")
            await Groups.AddToGroupAsync(Context.ConnectionId, "journalists");

        // Admin/nhân viên -> nhóm "admins" (nhận thông báo hoạt động quản trị, không lọt sang người dùng)
        if (!string.IsNullOrEmpty(adminId))
            await Groups.AddToGroupAsync(Context.ConnectionId, "admins");

        await Groups.AddToGroupAsync(Context.ConnectionId, "all");

        await base.OnConnectedAsync();
    }

    // Đây là luồng xử lý đánh dấu thông báo đã đọc
    // Luồng: 1) Tìm Notification theo id  2) Set IsRead=true, ReadAt=now  3) Lưu DB
    // Bảng: Notifications
    public async Task MarkAsRead(int notificationId)
    {
        var notif = await _db.Notifications.FindAsync(notificationId);
        if (notif != null)
        {
            notif.IsRead = true;
            notif.ReadAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }
    }
}
