using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Hubs;
using WisdomITNews.Models;

namespace WisdomITNews.Controllers;

[Route("api/chat")]
public class ChatController : Controller
{
    private readonly AppDbContext _db;
    private readonly IHubContext<ChatHub> _hub;

    public ChatController(AppDbContext db, IHubContext<ChatHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    // Đây là luồng xử lý xác định người dùng chat hiện tại từ Session (user hoặc admin)
    private (string type, int id, string name, string? avatar)? GetCurrentUser()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId.HasValue)
            return ("user", userId.Value,
                    HttpContext.Session.GetString("UserName") ?? "User",
                    HttpContext.Session.GetString("UserAvatar"));

        var adminId = HttpContext.Session.GetInt32("AdminId");
        if (adminId.HasValue)
            return ("admin", adminId.Value,
                    HttpContext.Session.GetString("AdminName") ?? "Admin",
                    null);

        return null;
    }

    // Lấy thông tin user hiện tại
    // Đây là luồng xử lý lấy thông tin người dùng chat hiện tại (id/tên/avatar)
    [HttpGet("me")]
    public IActionResult GetMe()
    {
        var user = GetCurrentUser();
        if (user == null) return Unauthorized();
        return Json(new { type = user.Value.type, id = user.Value.id, name = user.Value.name, avatar = user.Value.avatar });
    }

    // Danh sách nhóm chat của tôi
    // Đây là luồng xử lý lấy danh sách nhóm chat của tôi
    // Luồng: lấy nhóm mình là thành viên -> kèm tin nhắn cuối; với DM thì lấy tên/avatar đối phương
    //        -> sắp theo tin nhắn mới nhất
    // Bảng: ChatMembers, ChatGroups, ChatMessages, Users/Admins
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups()
    {
        var user = GetCurrentUser();
        if (user == null) return Unauthorized();

        var groupIds = await _db.ChatMembers
            .Where(m => m.MemberType == user.Value.type && m.MemberId == user.Value.id)
            .Select(m => m.GroupId)
            .ToListAsync();

        var groups = await _db.ChatGroups
            .Where(g => groupIds.Contains(g.Id))
            .Include(g => g.Members)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.Avatar,
                g.IsDirectMessage,
                MemberCount = g.Members.Count,
                Members = g.Members.Select(m => new { m.MemberType, m.MemberId }).ToList(),
                LastMessage = g.Messages.OrderByDescending(m => m.SentAt).Select(m => new
                {
                    m.Content,
                    m.MessageType,
                    m.SenderName,
                    m.SentAt
                }).FirstOrDefault()
            })
            .ToListAsync();

        // Lấy avatar + tên của đối phương trong DM
        var result = new List<object>();
        foreach (var g in groups)
        {
            string? otherName = null;
            string? otherAvatar = null;
            string? otherType = null;
            int? otherId = null;

            if (g.IsDirectMessage)
            {
                var other = g.Members.FirstOrDefault(m =>
                    !(m.MemberType == user.Value.type && m.MemberId == user.Value.id));
                if (other != null)
                {
                    otherType = other.MemberType;
                    otherId = other.MemberId;
                    if (other.MemberType == "admin")
                    {
                        var admin = await _db.Admins.FindAsync(other.MemberId);
                        otherName = admin?.FullName ?? "Admin";
                        otherAvatar = null;
                    }
                    else
                    {
                        var u = await _db.Users.FindAsync(other.MemberId);
                        otherName = u?.FullName ?? "User";
                        otherAvatar = u?.AvatarUrl;
                    }
                }
            }

            result.Add(new
            {
                g.Id,
                g.Name,
                g.Avatar,
                g.IsDirectMessage,
                g.MemberCount,
                g.LastMessage,
                OtherName = otherName,
                OtherAvatar = otherAvatar,
                OtherType = otherType,
                OtherId = otherId
            });
        }

        return Json(result.OrderByDescending(r =>
        {
            dynamic d = r;
            return d.LastMessage?.SentAt ?? DateTime.MinValue;
        }));
    }

    // Tạo nhóm mới
    // Đây là luồng xử lý tạo nhóm chat mới
    // Luồng: tạo ChatGroup -> thêm người tạo (role admin) + các thành viên được mời
    // Bảng: ChatGroups, ChatMembers
    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupRequest req)
    {
        var user = GetCurrentUser();
        if (user == null) return Unauthorized();

        var group = new ChatGroup
        {
            Name = req.Name?.Trim() ?? "Nhóm mới",
            CreatorType = user.Value.type,
            CreatorId = user.Value.id,
            IsDirectMessage = false
        };
        _db.ChatGroups.Add(group);
        await _db.SaveChangesAsync();

        // Thêm người tạo làm admin nhóm
        _db.ChatMembers.Add(new ChatMember
        {
            GroupId = group.Id,
            MemberType = user.Value.type,
            MemberId = user.Value.id,
            Role = "admin"
        });

        // Thêm các thành viên được mời
        if (req.MemberIds != null)
        {
            foreach (var mid in req.MemberIds)
            {
                _db.ChatMembers.Add(new ChatMember
                {
                    GroupId = group.Id,
                    MemberType = "user",
                    MemberId = mid,
                    Role = "member"
                });
            }
        }
        await _db.SaveChangesAsync();

        return Json(new { success = true, groupId = group.Id });
    }

    // Chat 1-1 (tạo hoặc mở)
    // Đây là luồng xử lý mở/tạo chat 1-1 (DM)
    // Luồng: tìm nhóm DM chung giữa 2 người; có rồi -> mở; chưa có -> tạo ChatGroup DM + 2 thành viên
    // Bảng: ChatMembers, ChatGroups
    [HttpPost("dm")]
    public async Task<IActionResult> DirectMessage([FromBody] DmRequest req)
    {
        var user = GetCurrentUser();
        if (user == null) return Unauthorized();

        // Tìm nhóm DM đã tồn tại
        var myGroups = await _db.ChatMembers
            .Where(m => m.MemberType == user.Value.type && m.MemberId == user.Value.id)
            .Select(m => m.GroupId).ToListAsync();

        var targetGroups = await _db.ChatMembers
            .Where(m => m.MemberType == req.TargetType && m.MemberId == req.TargetId)
            .Select(m => m.GroupId).ToListAsync();

        var commonDm = await _db.ChatGroups
            .Where(g => g.IsDirectMessage && myGroups.Contains(g.Id) && targetGroups.Contains(g.Id))
            .FirstOrDefaultAsync();

        if (commonDm != null)
            return Json(new { success = true, groupId = commonDm.Id });

        // Tạo DM mới
        var targetName = req.TargetType == "admin"
            ? (await _db.Admins.FindAsync(req.TargetId))?.FullName ?? "Admin"
            : (await _db.Users.FindAsync(req.TargetId))?.FullName ?? "User";

        var group = new ChatGroup
        {
            Name = $"{user.Value.name}, {targetName}",
            IsDirectMessage = true,
            CreatorType = user.Value.type,
            CreatorId = user.Value.id
        };
        _db.ChatGroups.Add(group);
        await _db.SaveChangesAsync();

        _db.ChatMembers.AddRange(
            new ChatMember { GroupId = group.Id, MemberType = user.Value.type, MemberId = user.Value.id, Role = "admin" },
            new ChatMember { GroupId = group.Id, MemberType = req.TargetType, MemberId = req.TargetId, Role = "member" }
        );
        await _db.SaveChangesAsync();

        return Json(new { success = true, groupId = group.Id });
    }

    // Lấy tin nhắn của nhóm
    // Đây là luồng xử lý lấy tin nhắn của nhóm (phân trang lùi để cuộn lên)
    // Luồng: kiểm tra là thành viên -> lấy 50 tin (Id < before nếu tải thêm) -> trả theo thứ tự thời gian
    // Bảng: ChatMembers, ChatMessages
    [HttpGet("groups/{groupId}/messages")]
    public async Task<IActionResult> GetMessages(int groupId, int before = 0, int take = 50)
    {
        var user = GetCurrentUser();
        if (user == null) return Unauthorized();

        var isMember = await _db.ChatMembers.AnyAsync(m =>
            m.GroupId == groupId && m.MemberType == user.Value.type && m.MemberId == user.Value.id);
        if (!isMember) return Forbid();

        var query = _db.ChatMessages
            .Where(m => m.GroupId == groupId);

        if (before > 0)
            query = query.Where(m => m.Id < before);

        var messages = await query
            .OrderByDescending(m => m.SentAt)
            .Take(take)
            .Select(m => new
            {
                m.Id,
                m.SenderType,
                m.SenderId,
                m.SenderName,
                m.SenderAvatar,
                m.Content,
                m.MessageType,
                m.FileUrl,
                m.FileName,
                m.ReplyToId,
                m.IsPinned,
                m.SentAt,
                ReplyTo = m.ReplyTo != null ? new { m.ReplyTo.SenderName, m.ReplyTo.Content } : null
            })
            .ToListAsync();

        return Json(messages.OrderBy(m => m.SentAt));
    }

    // Gửi tin nhắn
    // Đây là luồng xử lý gửi tin nhắn nhóm
    // Luồng: kiểm tra là thành viên -> tạo ChatMessage (text/ảnh/file, có thể trả lời) -> lưu DB
    //        -> phát realtime "ReceiveMessage" tới group
    // Bảng: ChatMembers, ChatMessages
    [HttpPost("groups/{groupId}/messages")]
    public async Task<IActionResult> SendMessage(int groupId, [FromBody] SendMessageRequest req)
    {
        var user = GetCurrentUser();
        if (user == null) return Unauthorized();

        var isMember = await _db.ChatMembers.AnyAsync(m =>
            m.GroupId == groupId && m.MemberType == user.Value.type && m.MemberId == user.Value.id);
        if (!isMember) return Forbid();

        var msg = new ChatMessage
        {
            GroupId = groupId,
            SenderType = user.Value.type,
            SenderId = user.Value.id,
            SenderName = user.Value.name,
            SenderAvatar = user.Value.avatar,
            Content = req.Content?.Trim() ?? "",
            MessageType = req.MessageType ?? "text",
            FileUrl = req.FileUrl,
            FileName = req.FileName,
            ReplyToId = req.ReplyToId
        };
        _db.ChatMessages.Add(msg);
        await _db.SaveChangesAsync();

        var payload = new
        {
            msg.Id,
            msg.GroupId,
            msg.SenderType,
            msg.SenderId,
            msg.SenderName,
            msg.SenderAvatar,
            msg.Content,
            msg.MessageType,
            msg.FileUrl,
            msg.FileName,
            msg.ReplyToId,
            msg.IsPinned,
            msg.SentAt,
            ReplyTo = msg.ReplyToId.HasValue
                ? await _db.ChatMessages.Where(m => m.Id == msg.ReplyToId).Select(m => new { m.SenderName, m.Content }).FirstOrDefaultAsync()
                : null
        };

        await _hub.Clients.Group($"chat_{groupId}").SendAsync("ReceiveMessage", payload);
        return Json(new { success = true, message = payload });
    }

    // Upload ảnh/file trong chat
    // Đây là luồng xử lý upload ảnh/file trong chat
    // Luồng: kiểm tra file ≤10MB -> lưu wwwroot/uploads/chat -> phân loại image/file -> trả URL
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        var user = GetCurrentUser();
        if (user == null) return Unauthorized();

        if (file == null || file.Length == 0)
            return Json(new { success = false, message = "Không có file" });

        if (file.Length > 10 * 1024 * 1024)
            return Json(new { success = false, message = "File quá lớn (tối đa 10MB)" });

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(file.FileName).ToLower();
        var fileName = $"chat_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        var imageExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var msgType = imageExts.Contains(ext) ? "image" : "file";

        return Json(new { success = true, url = $"/uploads/chat/{fileName}", name = file.FileName, type = msgType });
    }

    // Ghim/bỏ ghim tin nhắn
    // Đây là luồng xử lý ghim / bỏ ghim tin nhắn
    // Luồng: đảo IsPinned -> lưu -> phát realtime "MessagePinned"
    [HttpPost("messages/{msgId}/pin")]
    public async Task<IActionResult> TogglePin(int msgId)
    {
        var user = GetCurrentUser();
        if (user == null) return Unauthorized();

        var msg = await _db.ChatMessages.FindAsync(msgId);
        if (msg == null) return NotFound();

        msg.IsPinned = !msg.IsPinned;
        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"chat_{msg.GroupId}").SendAsync("MessagePinned", new { msg.Id, msg.IsPinned, msg.GroupId });
        return Json(new { success = true, isPinned = msg.IsPinned });
    }

    // Lấy thành viên nhóm
    // Đây là luồng xử lý lấy danh sách thành viên nhóm (kèm tên/avatar + trạng thái online)
    [HttpGet("groups/{groupId}/members")]
    public async Task<IActionResult> GetMembers(int groupId)
    {
        var user = GetCurrentUser();
        if (user == null) return Unauthorized();

        var members = await _db.ChatMembers.Where(m => m.GroupId == groupId).ToListAsync();
        var result = new List<object>();

        foreach (var m in members)
        {
            string name;
            string? avatar = null;
            if (m.MemberType == "admin")
            {
                var admin = await _db.Admins.FindAsync(m.MemberId);
                name = admin?.FullName ?? "Admin";
            }
            else
            {
                var u = await _db.Users.FindAsync(m.MemberId);
                name = u?.FullName ?? "User";
                avatar = u?.AvatarUrl;
            }
            result.Add(new { m.Id, m.MemberType, m.MemberId, name, avatar, m.Role, m.JoinedAt });
        }

        var online = ChatHub.GetOnlineUserKeys();
        return Json(result.Select(r =>
        {
            dynamic d = r;
            var key = $"{d.MemberType}_{d.MemberId}";
            return new { d.Id, d.MemberType, d.MemberId, d.name, d.avatar, d.Role, d.JoinedAt, isOnline = online.Contains(key) };
        }));
    }

    // Thêm thành viên
    // Đây là luồng xử lý thêm thành viên vào nhóm (chặn trùng)
    [HttpPost("groups/{groupId}/members")]
    public async Task<IActionResult> AddMember(int groupId, [FromBody] AddMemberRequest req)
    {
        var user = GetCurrentUser();
        if (user == null) return Unauthorized();

        var exists = await _db.ChatMembers.AnyAsync(m =>
            m.GroupId == groupId && m.MemberType == "user" && m.MemberId == req.UserId);
        if (exists) return Json(new { success = false, message = "Đã là thành viên" });

        _db.ChatMembers.Add(new ChatMember
        {
            GroupId = groupId,
            MemberType = "user",
            MemberId = req.UserId,
            Role = "member"
        });
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Rời nhóm
    // Đây là luồng xử lý rời nhóm chat (xóa bản ghi ChatMember của mình)
    [HttpPost("groups/{groupId}/leave")]
    public async Task<IActionResult> LeaveGroup(int groupId)
    {
        var user = GetCurrentUser();
        if (user == null) return Unauthorized();

        var member = await _db.ChatMembers.FirstOrDefaultAsync(m =>
            m.GroupId == groupId && m.MemberType == user.Value.type && m.MemberId == user.Value.id);
        if (member == null) return NotFound();

        _db.ChatMembers.Remove(member);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Tìm kiếm user để thêm vào nhóm / nhắn tin
    // Đây là luồng xử lý tìm người dùng để thêm vào nhóm / nhắn tin (cả User lẫn Admin, kèm online)
    [HttpGet("users/search")]
    public async Task<IActionResult> SearchUsers(string q = "")
    {
        var user = GetCurrentUser();
        if (user == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(q))
            return Json(new List<object>());

        var users = await _db.Users
            .Where(u => u.FullName.Contains(q) || u.Username.Contains(q))
            .Take(20)
            .Select(u => new { u.Id, u.FullName, u.Username, u.AvatarUrl, u.Role, type = "user" })
            .ToListAsync();

        var admins = await _db.Admins
            .Where(a => a.FullName.Contains(q) || a.Username.Contains(q))
            .Take(10)
            .Select(a => new { a.Id, a.FullName, a.Username, AvatarUrl = (string?)null, Role = a.Role, type = "admin" })
            .ToListAsync();

        var online = ChatHub.GetOnlineUserKeys();
        var result = users.Cast<object>().Concat(admins)
            .Select(u =>
            {
                dynamic d = u;
                var key = $"{d.type}_{d.Id}";
                return new { d.Id, d.FullName, d.Username, d.AvatarUrl, d.Role, d.type, isOnline = online.Contains(key) };
            });

        return Json(result);
    }

    // Lấy tin nhắn ghim
    // Đây là luồng xử lý lấy danh sách tin nhắn đã ghim của nhóm
    [HttpGet("groups/{groupId}/pinned")]
    public async Task<IActionResult> GetPinnedMessages(int groupId)
    {
        var msgs = await _db.ChatMessages
            .Where(m => m.GroupId == groupId && m.IsPinned)
            .OrderByDescending(m => m.SentAt)
            .Select(m => new { m.Id, m.SenderName, m.Content, m.MessageType, m.SentAt })
            .ToListAsync();
        return Json(msgs);
    }

    // Xóa tin nhắn (chỉ người gửi)
    // Đây là luồng xử lý xóa tin nhắn (chỉ người gửi mới được xóa)
    // Luồng: kiểm tra đúng người gửi -> xóa -> phát realtime "MessageDeleted"
    [HttpDelete("messages/{msgId}")]
    public async Task<IActionResult> DeleteMessage(int msgId)
    {
        var user = GetCurrentUser();
        if (user == null) return Unauthorized();

        var msg = await _db.ChatMessages.FindAsync(msgId);
        if (msg == null) return NotFound();
        if (msg.SenderType != user.Value.type || msg.SenderId != user.Value.id)
            return Forbid();

        var groupId = msg.GroupId;
        _db.ChatMessages.Remove(msg);
        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"chat_{groupId}").SendAsync("MessageDeleted", new { id = msgId, groupId });
        return Json(new { success = true });
    }
}

// Request models
public class CreateGroupRequest
{
    public string? Name { get; set; }
    public List<int>? MemberIds { get; set; }
}

public class DmRequest
{
    public string TargetType { get; set; } = "user";
    public int TargetId { get; set; }
}

public class SendMessageRequest
{
    public string? Content { get; set; }
    public string? MessageType { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public int? ReplyToId { get; set; }
}

public class AddMemberRequest
{
    public int UserId { get; set; }
}