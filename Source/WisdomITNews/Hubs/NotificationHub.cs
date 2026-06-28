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

        await Groups.AddToGroupAsync(Context.ConnectionId, "all");

        await base.OnConnectedAsync();
    }

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
