using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Hubs;
using WisdomITNews.Models;

namespace WisdomITNews.Controllers;

[Route("api/friends")]
public class FriendController : Controller
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hub;

    public FriendController(AppDbContext db, IHubContext<ChatHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    private int? GetUserId() => HttpContext.Session.GetInt32("UserId");

    // ===== KẾT BẠN =====

    /// <summary>Gửi lời mời kết bạn</summary>
    [HttpPost("request/{receiverId}")]
    public async Task<IActionResult> SendRequest(int receiverId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        if (userId == receiverId) return Json(new { success = false, message = "Không thể kết bạn với chính mình" });

        // Kiểm tra đã có friendship chưa
        var existing = await _db.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterId == userId && f.ReceiverId == receiverId) ||
            (f.RequesterId == receiverId && f.ReceiverId == userId));

        if (existing != null)
        {
            if (existing.Status == "accepted")
                return Json(new { success = false, message = "Đã là bạn bè" });
            if (existing.Status == "pending" && existing.RequesterId == userId)
                return Json(new { success = false, message = "Đã gửi lời mời rồi" });
            if (existing.Status == "pending" && existing.RequesterId == receiverId)
            {
                // Người kia đã gửi trước → tự động chấp nhận
                existing.Status = "accepted";
                existing.AcceptedAt = DateTime.Now;
                await _db.SaveChangesAsync();
                await NotifyFriendEvent(receiverId, userId.Value, "friend_accepted");
                return Json(new { success = true, message = "Đã chấp nhận lời mời kết bạn", status = "accepted" });
            }
            if (existing.Status == "rejected")
            {
                // Cho phép gửi lại
                existing.Status = "pending";
                existing.RequesterId = userId.Value;
                existing.ReceiverId = receiverId;
                existing.CreatedAt = DateTime.Now;
                existing.AcceptedAt = null;
                await _db.SaveChangesAsync();
                await NotifyFriendEvent(userId.Value, receiverId, "friend_request");
                return Json(new { success = true, message = "Đã gửi lại lời mời", status = "pending" });
            }
        }

        _db.Friendships.Add(new Friendship
        {
            RequesterId = userId.Value,
            ReceiverId = receiverId
        });
        await _db.SaveChangesAsync();
        await NotifyFriendEvent(userId.Value, receiverId, "friend_request");
        return Json(new { success = true, message = "Đã gửi lời mời kết bạn", status = "pending" });
    }

    /// <summary>Chấp nhận lời mời</summary>
    [HttpPost("accept/{requesterId}")]
    public async Task<IActionResult> Accept(int requesterId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var friendship = await _db.Friendships.FirstOrDefaultAsync(f =>
            f.RequesterId == requesterId && f.ReceiverId == userId && f.Status == "pending");
        if (friendship == null) return Json(new { success = false, message = "Không tìm thấy lời mời" });

        friendship.Status = "accepted";
        friendship.AcceptedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        await NotifyFriendEvent(userId.Value, requesterId, "friend_accepted");
        return Json(new { success = true });
    }

    /// <summary>Từ chối lời mời</summary>
    [HttpPost("reject/{requesterId}")]
    public async Task<IActionResult> Reject(int requesterId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var friendship = await _db.Friendships.FirstOrDefaultAsync(f =>
            f.RequesterId == requesterId && f.ReceiverId == userId && f.Status == "pending");
        if (friendship == null) return Json(new { success = false, message = "Không tìm thấy lời mời" });

        friendship.Status = "rejected";
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    /// <summary>Hủy kết bạn</summary>
    [HttpDelete("{friendId}")]
    public async Task<IActionResult> Unfriend(int friendId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var friendship = await _db.Friendships.FirstOrDefaultAsync(f =>
            f.Status == "accepted" &&
            ((f.RequesterId == userId && f.ReceiverId == friendId) ||
             (f.RequesterId == friendId && f.ReceiverId == userId)));
        if (friendship == null) return Json(new { success = false });

        _db.Friendships.Remove(friendship);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    /// <summary>Hủy kết bạn theo userId (dùng cho trang Profile)</summary>
    [HttpDelete("by-user/{targetId}")]
    public async Task<IActionResult> RemoveFriendByUser(int targetId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var friendship = await _db.Friendships.FirstOrDefaultAsync(f =>
            ((f.RequesterId == userId && f.ReceiverId == targetId) ||
             (f.RequesterId == targetId && f.ReceiverId == userId))
            && f.Status == "accepted");

        if (friendship == null) return Json(new { success = false, message = "Không tìm thấy" });

        _db.Friendships.Remove(friendship);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    /// <summary>Danh sách bạn bè</summary>
    [HttpGet("")]
    public async Task<IActionResult> GetFriends()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var friendships = await _db.Friendships
            .Include(f => f.Requester).Include(f => f.Receiver)
            .Where(f => f.Status == "accepted" &&
                (f.RequesterId == userId || f.ReceiverId == userId))
            .ToListAsync();

        var online = ChatHub.GetOnlineUserKeys();
        var result = friendships.Select(f =>
        {
            var friend = f.RequesterId == userId ? f.Receiver! : f.Requester!;
            return new
            {
                friend.Id,
                friend.FullName,
                friend.Username,
                friend.AvatarUrl,
                isOnline = online.Contains($"user_{friend.Id}"),
                since = f.AcceptedAt
            };
        }).OrderByDescending(f => f.isOnline).ThenBy(f => f.FullName);

        return Json(result);
    }

    /// <summary>Lời mời kết bạn đang chờ (nhận được)</summary>
    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var requests = await _db.Friendships
            .Include(f => f.Requester)
            .Where(f => f.ReceiverId == userId && f.Status == "pending")
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                f.Requester!.Id,
                f.Requester.FullName,
                f.Requester.Username,
                f.Requester.AvatarUrl,
                f.CreatedAt
            })
            .ToListAsync();

        return Json(requests);
    }

    /// <summary>Số lời mời đang chờ</summary>
    [HttpGet("requests/count")]
    public async Task<IActionResult> GetRequestCount()
    {
        var userId = GetUserId();
        if (userId == null) return Json(new { count = 0 });

        var count = await _db.Friendships
            .CountAsync(f => f.ReceiverId == userId && f.Status == "pending");
        return Json(new { count });
    }

    /// <summary>Gợi ý kết bạn: ưu tiên bạn chung, sau đó ngẫu nhiên</summary>
    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        // Lấy danh sách ID bạn bè hiện tại
        var friendIds = await _db.Friendships
            .Where(f => f.Status == "accepted" &&
                (f.RequesterId == userId || f.ReceiverId == userId))
            .Select(f => f.RequesterId == userId ? f.ReceiverId : f.RequesterId)
            .ToListAsync();

        // Lấy danh sách ID đã gửi/nhận lời mời pending
        var pendingIds = await _db.Friendships
            .Where(f => f.Status == "pending" &&
                (f.RequesterId == userId || f.ReceiverId == userId))
            .Select(f => f.RequesterId == userId ? f.ReceiverId : f.RequesterId)
            .ToListAsync();

        var excludeIds = friendIds.Concat(pendingIds).Append(userId.Value).Distinct().ToList();

        // Lấy tất cả user có thể gợi ý
        var candidates = await _db.Users
            .Where(u => !excludeIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Username, u.AvatarUrl })
            .ToListAsync();

        // Tính bạn chung cho mỗi candidate
        var online = ChatHub.GetOnlineUserKeys();
        var suggestions = new List<object>();

        foreach (var c in candidates)
        {
            var mutualCount = await _db.Friendships
                .CountAsync(f => f.Status == "accepted" &&
                    ((f.RequesterId == c.Id && friendIds.Contains(f.ReceiverId)) ||
                     (f.ReceiverId == c.Id && friendIds.Contains(f.RequesterId))));

            suggestions.Add(new
            {
                c.Id,
                c.FullName,
                c.Username,
                c.AvatarUrl,
                mutualFriends = mutualCount,
                isOnline = online.Contains($"user_{c.Id}")
            });
        }

        // Sắp xếp: bạn chung nhiều nhất → online → ngẫu nhiên
        var rng = new Random();
        var sorted = suggestions
            .OrderByDescending(s => ((dynamic)s).mutualFriends)
            .ThenByDescending(s => ((dynamic)s).isOnline)
            .ThenBy(_ => rng.Next())
            .Take(15)
            .ToList();

        return Json(sorted);
    }

    // ===== THEO DÕI (FOLLOW) =====

    /// <summary>Theo dõi người dùng</summary>
    [HttpPost("follow/{targetId}")]
    public async Task<IActionResult> Follow(int targetId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        if (userId == targetId) return Json(new { success = false, message = "Không thể tự theo dõi" });

        var exists = await _db.UserFollows.AnyAsync(f =>
            f.FollowerId == userId && f.FollowingId == targetId);
        if (exists) return Json(new { success = false, message = "Đã theo dõi rồi" });

        _db.UserFollows.Add(new UserFollow
        {
            FollowerId = userId.Value,
            FollowingId = targetId
        });
        await _db.SaveChangesAsync();
        await NotifyFriendEvent(userId.Value, targetId, "new_follower");
        return Json(new { success = true });
    }

    /// <summary>Bỏ theo dõi</summary>
    [HttpDelete("follow/{targetId}")]
    public async Task<IActionResult> Unfollow(int targetId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var follow = await _db.UserFollows.FirstOrDefaultAsync(f =>
            f.FollowerId == userId && f.FollowingId == targetId);
        if (follow == null) return Json(new { success = false });

        _db.UserFollows.Remove(follow);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    /// <summary>Lấy trạng thái quan hệ với một user</summary>
    [HttpGet("status/{targetId}")]
    public async Task<IActionResult> GetRelationStatus(int targetId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var friendship = await _db.Friendships.FirstOrDefaultAsync(f =>
            (f.RequesterId == userId && f.ReceiverId == targetId) ||
            (f.RequesterId == targetId && f.ReceiverId == userId));

        var iFollow = await _db.UserFollows.AnyAsync(f => f.FollowerId == userId && f.FollowingId == targetId);
        var followsMe = await _db.UserFollows.AnyAsync(f => f.FollowerId == targetId && f.FollowingId == userId);

        string friendStatus = "none";
        if (friendship != null)
        {
            if (friendship.Status == "accepted") friendStatus = "friends";
            else if (friendship.Status == "pending" && friendship.RequesterId == userId) friendStatus = "sent";
            else if (friendship.Status == "pending" && friendship.ReceiverId == userId) friendStatus = "received";
        }

        return Json(new { friendStatus, iFollow, followsMe });
    }

    /// <summary>Danh sách bạn bè online (cho chat sidebar)</summary>
    [HttpGet("online")]
    public async Task<IActionResult> GetOnlineFriendsAndUsers()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var online = ChatHub.GetOnlineUserKeys();
        var onlineUserIds = online
            .Where(k => k.StartsWith("user_"))
            .Select(k => int.TryParse(k.Replace("user_", ""), out var id) ? id : 0)
            .Where(id => id > 0 && id != userId)
            .ToList();

        // Lấy bạn bè
        var friendIds = await _db.Friendships
            .Where(f => f.Status == "accepted" &&
                (f.RequesterId == userId || f.ReceiverId == userId))
            .Select(f => f.RequesterId == userId ? f.ReceiverId : f.RequesterId)
            .ToListAsync();

        // Bạn bè đang online
        var onlineFriendIds = onlineUserIds.Where(id => friendIds.Contains(id)).ToList();
        var onlineFriends = await _db.Users
            .Where(u => onlineFriendIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Username, u.AvatarUrl, isFriend = true })
            .ToListAsync();

        // Người khác đang online (không phải bạn)
        var otherOnlineIds = onlineUserIds.Where(id => !friendIds.Contains(id)).ToList();
        var otherOnline = await _db.Users
            .Where(u => otherOnlineIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Username, u.AvatarUrl, isFriend = false })
            .ToListAsync();

        return Json(new { friends = onlineFriends, others = otherOnline });
    }

    // ===== HELPER =====
    private async Task NotifyFriendEvent(int fromId, int toId, string eventType)
    {
        var fromUser = await _db.Users.FindAsync(fromId);
        var key = $"user_{toId}";
        await _hub.Clients.All.SendAsync("FriendEvent", new
        {
            type = eventType,
            fromId,
            fromName = fromUser?.FullName ?? "User",
            fromAvatar = fromUser?.AvatarUrl,
            toId
        });
    }
}