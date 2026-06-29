namespace WisdomITNews.Models;

/// <summary>
/// Một video (hiện dùng dữ liệu MẪU). Sau này thay nguồn bằng DB / RSS / YouTube API.
/// </summary>
public class VideoItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string YouTubeId { get; set; } = ""; // id video trên YouTube để nhúng
    public string Source { get; set; } = "";
    public int Views { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.Now;
    public string VideoType { get; set; } = "youtube";
    public string? VideoUrl { get; set; }

    public bool IsUpload => VideoType == "upload";
    public string Thumbnail => IsUpload
        ? (VideoUrl ?? "")
        : $"https://img.youtube.com/vi/{YouTubeId}/hqdefault.jpg";
    public string EmbedUrl  => $"https://www.youtube.com/embed/{YouTubeId}";
}

/// <summary>
/// Dữ liệu video MẪU (placeholder). Thay danh sách này bằng dữ liệu thật sau.
/// </summary>
public static class VideoSampleData
{
    public static readonly List<VideoItem> Items = new()
    {
        new() { Id = 1, Title = "Toàn cảnh sự kiện ra mắt Llama 4 của Meta AI",        YouTubeId = "jNQXAC9IVRw", Source = "Wisdom TV",  Views = 21000, PublishedAt = DateTime.Now.AddMinutes(-12) },
        new() { Id = 2, Title = "Hướng dẫn dùng ChatGPT hiệu quả cho lập trình viên",  YouTubeId = "dQw4w9WgXcQ", Source = "VTV24",      Views = 15400, PublishedAt = DateTime.Now.AddMinutes(-40) },
        new() { Id = 3, Title = "Đánh giá GPU mới nhất 2026: hiệu năng AI vượt trội",  YouTubeId = "9bZkp7q19f0", Source = "ZNews",      Views = 11300, PublishedAt = DateTime.Now.AddHours(-1) },
        new() { Id = 4, Title = "Bên trong trung tâm dữ liệu lớn nhất Việt Nam",       YouTubeId = "kJQP7kiw5Fk", Source = "Tiền Phong", Views = 9800,  PublishedAt = DateTime.Now.AddHours(-2) },
        new() { Id = 5, Title = "Cách tự bảo vệ trước mã độc và lừa đảo mạng 2026",    YouTubeId = "JGwWNGJdvx8", Source = "VietNamNet", Views = 8700,  PublishedAt = DateTime.Now.AddHours(-3) },
        new() { Id = 6, Title = "Review những chiếc laptop lập trình đáng mua nhất",   YouTubeId = "OPf0YbXqDm0", Source = "Wisdom TV",  Views = 7600,  PublishedAt = DateTime.Now.AddHours(-5) },
        new() { Id = 7, Title = "Demo robot AI Tesla Optimus Gen 3 trong nhà máy",     YouTubeId = "fJ9rUzIMcZQ", Source = "ZNews",      Views = 13500, PublishedAt = DateTime.Now.AddHours(-7) },
        new() { Id = 8, Title = "5G và tương lai kết nối tốc độ cao tại Việt Nam",     YouTubeId = "60ItHLz5WEA", Source = "VTC News",   Views = 6400,  PublishedAt = DateTime.Now.AddHours(-9) },
        new() { Id = 9, Title = "Khởi nghiệp công nghệ: bài học từ các startup Việt",  YouTubeId = "CevxZvSJLk8", Source = "Tiền Phong", Views = 5200,  PublishedAt = DateTime.Now.AddHours(-11) },
    };
}

// ===== Entity Video (lưu DB) — YouTube hoặc upload file =====
public class Video
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string YouTubeId { get; set; } = "";
    public string? Source { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "published";
    public int Views { get; set; }
    public int? CreatedByAdminId { get; set; }
    public int? CreatedByUserId { get; set; }
    public string? VideoUrl { get; set; }
    public string? VideoType { get; set; } = "youtube";
    public long? FileSize { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime PublishedAt { get; set; } = DateTime.Now;

    public bool IsUpload => VideoType == "upload";
    public string Thumbnail => IsUpload
        ? (VideoUrl ?? "")
        : $"https://img.youtube.com/vi/{YouTubeId}/hqdefault.jpg";
    public string EmbedUrl  => $"https://www.youtube.com/embed/{YouTubeId}";
}

// Tách YouTube video-id từ link (watch?v=, youtu.be/, embed/, shorts/) hoặc nhận thẳng id 11 ký tự
public static class YouTubeHelper
{
    public static string? ExtractId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        input = input.Trim();
        if (System.Text.RegularExpressions.Regex.IsMatch(input, "^[A-Za-z0-9_-]{11}$")) return input;
        var m = System.Text.RegularExpressions.Regex.Match(input,
            @"(?:youtu\.be/|youtube\.com/(?:watch\?v=|embed/|shorts/|v/)|[?&]v=)([A-Za-z0-9_-]{11})");
        return m.Success ? m.Groups[1].Value : null;
    }
}

// ===== Bình luận video (PHẲNG — KHÔNG đệ quy; chỉ cho trả lời 1 cấp) =====
// ParentId = null  -> bình luận gốc
// ParentId có giá trị -> là 1 trả lời, luôn trỏ tới bình luận GỐC (controller ép về gốc nên không bao giờ quá 1 cấp)
public class VideoComment
{
    public int Id { get; set; }
    public int VideoId { get; set; }
    public int? ParentId { get; set; }
    public string AuthorName { get; set; } = "";
    public string? AuthorEmail { get; set; }
    public string Content { get; set; } = "";
    public int? UserId { get; set; }
    public string Status { get; set; } = "published"; // published / rejected (hiện ngay sau khi AI lọc)
    public int Likes { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Video? Video { get; set; }
}
