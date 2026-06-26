namespace WisdomITNews.Services;

/// <summary>
/// Validate + lưu / xóa file video upload (mp4, webm, mov).
/// </summary>
public class VideoUploadService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<VideoUploadService> _logger;

    private static readonly string[] AllowedExt = { ".mp4", ".webm", ".mov" };
    public const long MaxSize = 500L * 1024 * 1024; // 500MB

    public VideoUploadService(IWebHostEnvironment env, ILogger<VideoUploadService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public class UploadResult
    {
        public bool Success { get; set; }
        public string? RelativePath { get; set; }
        public long FileSize { get; set; }
        public string? Error { get; set; }
    }

    public async Task<UploadResult> SaveAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return new UploadResult { Success = false, Error = "Không có file video." };

        if (file.Length > MaxSize)
            return new UploadResult { Success = false, Error = "File quá lớn (tối đa 500MB)." };

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExt.Contains(ext))
            return new UploadResult { Success = false, Error = "Chỉ chấp nhận .mp4, .webm, .mov." };

        try
        {
            var relDir = "uploads/videos";
            var absDir = Path.Combine(_env.WebRootPath, relDir);
            Directory.CreateDirectory(absDir);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var absPath = Path.Combine(absDir, fileName);
            var relPath = $"/{relDir}/{fileName}";

            await using var stream = new FileStream(absPath, FileMode.Create);
            await file.CopyToAsync(stream);

            return new UploadResult { Success = true, RelativePath = relPath, FileSize = file.Length };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VideoUploadService.SaveAsync failed");
            return new UploadResult { Success = false, Error = "Không lưu được file video: " + ex.Message };
        }
    }

    public void DeletePhysicalFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        try
        {
            var path = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var abs = Path.Combine(_env.WebRootPath, path);
            if (File.Exists(abs)) File.Delete(abs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VideoUploadService.DeletePhysicalFile failed for {Path}", relativePath);
        }
    }
}
