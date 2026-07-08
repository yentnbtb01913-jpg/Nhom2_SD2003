using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Services;

/// <summary>Kiểm tra quyền đọc nội dung Premium của người dùng.</summary>
public static class PremiumAccess
{
    // Status IN (Trial, Active) AND ConfirmedAt != null AND EndDate > now
    public static async Task<bool> HasAsync(AppDbContext db, int? userId)
    {
        if (userId == null) return false;
        var now = DateTime.Now;
        return await db.UserSubscriptions.AnyAsync(s =>
            s.UserId == userId.Value &&
            (s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active) &&
            s.ConfirmedAt != null && s.EndDate > now);
    }

    // Rút gọn nội dung khi bị khóa: bỏ HTML, gộp khoảng trắng, cắt theo số ký tự.
    public static string Preview(string? html, int limit = 320)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, "\\s+", " ").Trim();
        if (text.Length <= limit) return text;
        return text.Substring(0, limit).TrimEnd() + "…";
    }
}
