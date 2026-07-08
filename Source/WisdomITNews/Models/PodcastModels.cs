namespace WisdomITNews.Models;

// Bản ghi audio/podcast: file upload hoặc audio TTS tạo từ bài viết.
public class Podcast
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string FilePath { get; set; } = "";          // đường dẫn tương đối, vd /uploads/audio/2026/07/xxx.wav
    public int DurationSeconds { get; set; }
    public long FileSizeBytes { get; set; }
    public int? ArticleId { get; set; }                 // FK tới Article (tùy chọn)
    public int? UploadedByUserId { get; set; }          // id người tạo (Admin.Id hoặc User.Id tùy loại)
    public string UploadedByType { get; set; } = "";    // Admin / NhanVien / Journalist
    public bool IsAiGenerated { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public Article? Article { get; set; }
}
