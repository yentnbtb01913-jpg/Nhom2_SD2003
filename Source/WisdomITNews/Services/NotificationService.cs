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

    // Đây là luồng xử lý gửi thông báo hệ thống cho tất cả người dùng
    // Luồng: 1) Tạo Notification (Type="system", mã NTBxxxx)  2) Lưu DB
    //        3) Phát realtime "ReceiveNotification" tới Clients.All
    // Bảng: Notifications
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

    // Gửi thông báo tới MỘT người dùng cụ thể (vào hộp thư + realtime).
    public async Task SendToUserAsync(int userId, string title, string content,
        string type = "ad_booking", string icon = "bullhorn", string iconColor = "#0e7d85")
    {
        var notif = new Notification
        {
            Code = GenerateCode(),
            Title = title,
            Content = content,
            Type = type,
            Icon = icon,
            IconColor = iconColor,
            TargetType = "user",
            TargetUserId = userId,
            SentBy = "system",
            CreatedAt = DateTime.Now
        };
        _db.Notifications.Add(notif);
        await _db.SaveChangesAsync();
        await _hub.Clients.Group($"user_{userId}").SendAsync("ReceiveNotification", notif);
    }

    // Đây là luồng xử lý gửi thông báo bình luận vi phạm (AI phát hiện)
    // Luồng: 1) Tạo Notification Type="ai_violation", TargetUserId=userId, lưu nội dung vi phạm
    //        2) Lưu DB  3) Phát realtime tới group "user_{userId}"
    // Bảng: Notifications
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

    // Đây là luồng xử lý gửi thông báo bài viết bị từ chối
    // Luồng: 1) Tạo Notification Type="article_rejected", TargetUserId=authorUserId, RelatedArticleId
    //        2) Lưu DB  3) Phát realtime tới group "user_{authorUserId}"
    // Bảng: Notifications
    // rejectedBy = tác nhân đã từ chối (tên người duyệt, hoặc "AI (Gemini)")
    public async Task SendArticleRejectedAsync(int authorUserId, int articleId, string articleTitle, string reason, string rejectedBy = "Ban biên tập")
    {
        var notif = new Notification
        {
            Code = GenerateCode(),
            Title = "Bài viết bị từ chối",
            Content = $"Bài viết \"{articleTitle}\" của bạn đã bị {rejectedBy} từ chối.",
            Type = "article_rejected",
            Icon = "ban",
            IconColor = "#dc2626",
            TargetType = "user",
            TargetUserId = authorUserId,
            ViolationReason = reason,
            RelatedArticleId = articleId,
            SentBy = rejectedBy,
            CreatedAt = DateTime.Now
        };
        _db.Notifications.Add(notif);
        await _db.SaveChangesAsync();
        await _hub.Clients.Group($"user_{authorUserId}").SendAsync("ReceiveNotification", notif);
    }

    // Đây là luồng xử lý gửi thông báo video bị từ chối
    // Luồng: tạo Notification Type="video_rejected", RelatedVideoId -> lưu DB -> phát tới "user_{adminId}"
    // Bảng: Notifications
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

    // Đây là luồng xử lý gửi thông báo tới một email cụ thể
    // Luồng: tạo Notification -> lưu DB -> phát realtime tới group "email_{email}"
    // Bảng: Notifications
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

    // Đây là luồng xử lý gửi thông báo cho tất cả nhà báo
    // Luồng: tạo Notification -> lưu DB -> phát realtime tới group "journalists"
    // Bảng: Notifications
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

    // Đây là luồng xử lý sinh mã thông báo (NTBxxxx)
    private string GenerateCode()
    {
        var count = _db.Notifications.Count() + 1;
        return $"NTB{count:D4}";
    }
}
