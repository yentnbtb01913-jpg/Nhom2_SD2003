using System.Diagnostics;
using System.Text.RegularExpressions;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Services;

/// <summary>
/// Lưu file audio upload (.mp3/.wav) và tạo audio TTS từ bài viết bằng Piper.
/// </summary>
public class PodcastService
{
    private readonly IWebHostEnvironment _env;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<PodcastService> _logger;

    private static readonly string[] AllowedExt = { ".mp3", ".wav" };
    private const long MaxSize = 50L * 1024 * 1024;   // 50MB
    private const int MaxTtsChars = 20000;            // giới hạn text đưa vào Piper

    public PodcastService(IWebHostEnvironment env, AppDbContext db, IConfiguration config, ILogger<PodcastService> logger)
    {
        _env = env;
        _db = db;
        _config = config;
        _logger = logger;
    }

    public class PodcastResult
    {
        public bool Success { get; set; }
        public Podcast? Podcast { get; set; }
        public string? Error { get; set; }
    }

    // ===== Lưu file audio upload =====
    // Đây là luồng xử lý upload file podcast (audio) thủ công
    // Luồng: kiểm tra file audio -> lưu vào wwwroot/uploads/audio -> tạo Podcast (gắn Article tùy chọn)
    public async Task<PodcastResult> SaveUploadAsync(IFormFile file, string? title, string? description,
        int? articleId, int? userId, string uploadedByType)
    {
        if (file == null || file.Length == 0)
            return new PodcastResult { Error = "Không có file" };
        if (file.Length > MaxSize)
            return new PodcastResult { Error = "File quá lớn (>50MB)" };

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExt.Contains(ext))
            return new PodcastResult { Error = "Chỉ chấp nhận file .mp3 hoặc .wav" };

        try
        {
            var (relPath, absPath) = BuildPath(ext);
            using (var stream = new FileStream(absPath, FileMode.Create))
                await file.CopyToAsync(stream);

            var podcast = new Podcast
            {
                Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(file.FileName) : title.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                FilePath = relPath,
                DurationSeconds = ext == ".wav" ? TryGetWavDuration(absPath) : 0,   // TODO: đọc duration mp3 cần thư viện riêng
                FileSizeBytes = file.Length,
                ArticleId = articleId,
                UploadedByUserId = userId,
                UploadedByType = uploadedByType,
                IsAiGenerated = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _db.Podcasts.Add(podcast);
            await _db.SaveChangesAsync();
            return new PodcastResult { Success = true, Podcast = podcast };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PodcastService.SaveUploadAsync failed");
            return new PodcastResult { Error = "Lỗi lưu file: " + ex.Message };
        }
    }

    // ===== Tạo audio TTS từ bài viết bằng Piper =====
    // Đây là luồng xử lý tạo podcast tự động từ bài viết (TTS)
    // Luồng: lấy nội dung bài -> chạy Piper (text-to-speech tiếng Việt) sinh file audio -> tạo Podcast gắn bài
    public async Task<PodcastResult> GenerateFromArticleAsync(int articleId, int? userId, string uploadedByType)
    {
        var article = await _db.Articles.FindAsync(articleId);
        if (article == null) return new PodcastResult { Error = "Không tìm thấy bài viết" };

        var text = StripHtml(article.Content ?? "");
        if (string.IsNullOrWhiteSpace(text)) return new PodcastResult { Error = "Bài viết không có nội dung để đọc" };
        if (text.Length > MaxTtsChars) text = text.Substring(0, MaxTtsChars);

        // Binary + model đọc từ cấu hình (KHÔNG hardcode). Mặc định: 'piper' + model tiếng Việt.
        var binary = _config["Piper:BinaryPath"];
        if (string.IsNullOrWhiteSpace(binary)) binary = "piper";
        var model = _config["Piper:ModelPath"];
        if (string.IsNullOrWhiteSpace(model)) model = "vi_VN-voice-medium.onnx";

        var (relPath, absPath) = BuildPath(".wav");

        try
        {
            // TODO: điều chỉnh tham số dòng lệnh theo bản Piper thực tế đã cài trên máy.
            // Cách gọi phổ biến của Piper CLI: đưa text qua stdin, xuất WAV ra --output_file.
            //   echo "nội dung" | piper --model vi_VN-voice-medium.onnx --output_file out.wav
            var psi = new ProcessStartInfo
            {
                FileName = binary,
                Arguments = $"--model \"{model}\" --output_file \"{absPath}\"",
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) throw new Exception("Không khởi động được tiến trình Piper");

            var errTask = proc.StandardError.ReadToEndAsync();   // đọc song song để tránh deadlock
            await proc.StandardInput.WriteAsync(text);
            proc.StandardInput.Close();
            await proc.WaitForExitAsync();
            var stderr = await errTask;

            if (proc.ExitCode != 0 || !File.Exists(absPath) || new FileInfo(absPath).Length == 0)
                throw new Exception($"Piper trả lỗi (exit {proc.ExitCode}): {stderr}");

            var size = new FileInfo(absPath).Length;
            var podcast = new Podcast
            {
                Title = "Audio: " + article.Title,
                Description = "Tạo tự động từ bài viết bằng Piper TTS.",
                FilePath = relPath,
                DurationSeconds = TryGetWavDuration(absPath),
                FileSizeBytes = size,
                ArticleId = articleId,
                UploadedByUserId = userId,
                UploadedByType = uploadedByType,
                IsAiGenerated = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _db.Podcasts.Add(podcast);
            LogTts(articleId, true, $"Tạo audio thành công ({size} bytes): {relPath}", null);
            await _db.SaveChangesAsync();
            return new PodcastResult { Success = true, Podcast = podcast };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PodcastService TTS Piper failed (articleId={ArticleId})", articleId);
            LogTts(articleId, false, null, ex.Message);
            try { await _db.SaveChangesAsync(); } catch { /* ghi log best-effort */ }
            return new PodcastResult { Error = "Không tạo được audio bằng Piper: " + ex.Message };
        }
    }

    // ===== Helpers =====
    private (string relPath, string absPath) BuildPath(string ext)
    {
        var now = DateTime.Now;
        var relDir = $"uploads/audio/{now.Year:0000}/{now.Month:00}";
        var absDir = Path.Combine(_env.WebRootPath, relDir);
        Directory.CreateDirectory(absDir);
        var name = $"{Guid.NewGuid():N}{ext}";
        return ($"/{relDir}/{name}", Path.Combine(absDir, name));
    }

    private void LogTts(int? articleId, bool success, string? result, string? error)
    {
        try
        {
            _db.AILogs.Add(new AILog
            {
                ArticleId = articleId,
                Action = "tts_piper",
                ResultText = result,
                ErrorMsg = error,
                ModelUsed = "piper",
                IsSuccess = success,
                CreatedAt = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AILog tts_piper add failed");
        }
    }

    private static string StripHtml(string html)
    {
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    // Đọc duration WAV PCM canonical từ header (bytes). mp3 chưa hỗ trợ -> 0.
    private static int TryGetWavDuration(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            var head = new byte[44];
            if (fs.Read(head, 0, 44) < 44) return 0;
            if (head[0] != (byte)'R' || head[1] != (byte)'I' || head[2] != (byte)'F' || head[3] != (byte)'F') return 0;
            int byteRate = BitConverter.ToInt32(head, 28);   // bytes/giây
            int dataSize = BitConverter.ToInt32(head, 40);   // kích thước chunk data (WAV canonical, fmt=16)
            if (byteRate <= 0 || dataSize <= 0) return 0;
            return dataSize / byteRate;
        }
        catch
        {
            return 0;
        }
    }
}
