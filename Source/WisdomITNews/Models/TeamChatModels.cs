namespace WisdomITNews.Models;

// Tin nhắn chat nội bộ ban quản lý (Admin + Nhân viên + Nhà báo).
// Dùng CHUNG cho phòng chung ("team") lẫn nhắn riêng DM (ConversationKey = "dm:...").
public class TeamChatMessage
{
    public int Id { get; set; }
    public string ConversationKey { get; set; } = "team";
    public bool IsGroup { get; set; } = true;
    public string SenderRole { get; set; } = "";      // admin / nhanvien / journalist
    public int SenderId { get; set; }
    public string SenderName { get; set; } = "";
    public string? RecipientRole { get; set; }         // chỉ dùng cho DM
    public int? RecipientId { get; set; }
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public static class TeamChatKeys
{
    public const string Group = "team";

    // Khóa hội thoại DM chuẩn hóa (không phụ thuộc thứ tự 2 người).
    public static string Dm(string roleA, int idA, string roleB, int idB)
    {
        var a = roleA + ":" + idA;
        var b = roleB + ":" + idB;
        var arr = new[] { a, b };
        System.Array.Sort(arr, System.StringComparer.Ordinal);
        return "dm:" + arr[0] + "_" + arr[1];
    }
}
