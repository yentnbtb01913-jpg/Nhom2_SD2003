using Microsoft.AspNetCore.SignalR;
using WisdomITNews.Data;
using WisdomITNews.Models;
using WisdomITNews.Hubs;

namespace WisdomITNews.Services;

public class NotificationService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hub;

    public NotificationService(AppDbContext db, IHubContext<NotificationHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task SendSystemAsync(string title, string content, string icon = "bell", string iconColor = "#159aa3")
    {
        var notif = new Notification
        {
            Code = GenerateCode(),
            Title = title,
            Content = content,
            Type = "system",
            Icon = icon,
            IconColor = iconColor,
            TargetType = "all",
            SentBy = "system",
            CreatedAt = DateTime.Now
        };
        _db.Notifications.Add(notif);
        await _db.SaveChangesAsync();
        await _hub.Clients.All.SendAsync("ReceiveNotification", notif);
    }

    public async Task SendAiViolationAsync(int userId, string violationContent, int? commentId = null)
    {
        var notif = new Notification
        {
            Code = GenerateCode(),
            Title = "Bình luận vi phạm bị AI phát hiện",
            Content = "Bình luận của bạn chứa nội dung vi phạm và đã bị hệ thống AI gắn cờ.",
            Type = "ai_violation",
            Icon = "robot",
            IconColor = "#f59e0b",
            TargetType = "user",
            TargetUserId = userId,
            ViolationContent = violationContent,
            RelatedCommentId = commentId,
            SentBy = "system",
            CreatedAt = DateTime.Now
        };
        _db.Notifications.Add(notif);
        await _db.SaveChangesAsync();
        await _hub.Clients.Group($"user_{userId}").SendAsync("ReceiveNotification", notif);
    }

    public async Task SendArticleRejectedAsync(int authorUserId, int articleId, string articleTitle, string reason)
    {
        var notif = new Notification
        {
            Code = GenerateCode(),
            Title = "Bài viết bị từ chối",
            Content = $"Bài viết \"{articleTitle}\" của bạn đã bị từ chối.",
            Type = "article_rejected",
            Icon = "ban",
            IconColor = "#dc2626",
            TargetType = "user",
            TargetUserId = authorUserId,
            ViolationReason = reason,
            RelatedArticleId = articleId,
            SentBy = "admin",
            CreatedAt = DateTime.Now
        };
        _db.Notifications.Add(notif);
        await _db.SaveChangesAsync();
        await _hub.Clients.Group($"user_{authorUserId}").SendAsync("ReceiveNotification", notif);
    }

    public async Task SendVideoRejectedAsync(int adminId, int videoId, string videoTitle, string reason)
    {
        var notif = new Notification
        {
            Code = GenerateCode(),
            Title = "Video bị từ chối",
            Content = $"Video \"{videoTitle}\" đã bị từ chối.",
            Type = "video_rejected",
            Icon = "video",
            IconColor = "#dc2626",
            TargetType = "user",
            TargetUserId = adminId,
            ViolationReason = reason,
            RelatedVideoId = videoId,
            SentBy = "admin",
            CreatedAt = DateTime.Now
        };
        _db.Notifications.Add(notif);
        await _db.SaveChangesAsync();
        await _hub.Clients.Group($"user_{adminId}").SendAsync("ReceiveNotification", notif);
    }

    public async Task SendToEmailAsync(string email, string title, string content)
    {
        var notif = new Notification
        {
            Code = GenerateCode(),
            Title = title,
            Content = content,
            Type = "custom",
            Icon = "user",
            IconColor = "#6366f1",
            TargetType = "email",
            TargetEmail = email,
            SentBy = "admin",
            CreatedAt = DateTime.Now
        };
        _db.Notifications.Add(notif);
        await _db.SaveChangesAsync();
        await _hub.Clients.Group($"email_{email}").SendAsync("ReceiveNotification", notif);
    }

    public async Task SendToJournalistsAsync(string title, string content)
    {
        var notif = new Notification
        {
            Code = GenerateCode(),
            Title = title,
            Content = content,
            Type = "custom",
            Icon = "bell",
            IconColor = "#0891b2",
            TargetType = "journalist",
            TargetRole = "Journalist",
            SentBy = "admin",
            CreatedAt = DateTime.Now
        };
        _db.Notifications.Add(notif);
        await _db.SaveChangesAsync();
        await _hub.Clients.Group("journalists").SendAsync("ReceiveNotification", notif);
    }

    private string GenerateCode()
    {
        var count = _db.Notifications.Count() + 1;
        return $"NTB{count:D4}";
    }
}
