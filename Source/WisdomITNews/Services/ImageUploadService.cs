using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace WisdomITNews.Services;

/// <summary>
/// [K] Validate + resize + lưu ảnh upload cho Article.
/// </summary>
public class ImageUploadService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ImageUploadService> _logger;

    private static readonly string[] AllowedExt = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxSize = 5 * 1024 * 1024; // 5MB
    private const int MaxWidth = 1200;

    public ImageUploadService(IWebHostEnvironment env, ILogger<ImageUploadService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public class UploadResult
    {
        public bool Success { get; set; }
        public string? RelativePath { get; set; }
        public string? Error { get; set; }
    }

    // Đây là luồng xử lý upload ảnh cho bài viết (kiểm tra + resize + lưu)
    // Luồng: 1) Kiểm tra có file, kích thước ≤5MB, đuôi jpg/png/webp
    //        2) Lưu vào wwwroot/uploads/articles/{năm}/{tháng}/{guid}{ext}
    //        3) Resize giữ tỷ lệ nếu rộng >1200px -> lưu file -> trả đường dẫn tương đối
    public async Task<UploadResult> SaveAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return new UploadResult { Success = false, Error = "Không có file" };

        if (file.Length > MaxSize)
            return new UploadResult { Success = false, Error = "File quá lớn (>5MB)" };

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExt.Contains(ext))
            return new UploadResult { Success = false, Error = "Chỉ chấp nhận jpg, png, webp" };

        try
        {
            // Path: wwwroot/uploads/articles/{year}/{month}/{guid}{ext}
            var now = DateTime.Now;
            var relDir = $"uploads/articles/{now.Year:0000}/{now.Month:00}";
            var absDir = Path.Combine(_env.WebRootPath, relDir);
            Directory.CreateDirectory(absDir);

            var fileName = $"{Guid.NewGuid():N}{(ext == ".jpeg" ? ".jpg" : ext)}";
            var absPath  = Path.Combine(absDir, fileName);
            var relPath  = $"/{relDir}/{fileName}";

            using var stream = file.OpenReadStream();
            using var image  = await Image.LoadAsync(stream);

            // Resize giữ tỷ lệ, max width 1200
            if (image.Width > MaxWidth)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(MaxWidth, 0)
                }));
            }

            await image.SaveAsync(absPath);

            return new UploadResult { Success = true, RelativePath = relPath };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImageUploadService.SaveAsync failed");
            return new UploadResult { Success = false, Error = "Không xử lý được ảnh: " + ex.Message };
        }
    }
}
