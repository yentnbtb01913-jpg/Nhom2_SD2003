using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;
using WisdomITNews.Services;

namespace WisdomITNews.Controllers;

public class JournalistController : Controller
{
    private readonly AppDbContext _db;
    private readonly AIService _ai;
    private readonly VideoUploadService _videoUpload;
    private readonly ILogger<JournalistController> _logger;

    public JournalistController(AppDbContext db, AIService ai, VideoUploadService videoUpload, ILogger<JournalistController> logger)
    {
        _db = db;
        _ai = ai;
        _videoUpload = videoUpload;
        _logger = logger;
    }

    // ===== AI Moderation (mirror AdminController) =====
    private async Task ApplyAIModerationAsync(Article article)
    {
        try
        {
            var combined = $"{article.Title}\n\n{article.Summary}\n\n{article.Content}";
            var result = await _ai.ModerateContentAsync(combined);

            _db.AILogs.Add(new AILog
            {
                ArticleId  = article.Id == 0 ? null : article.Id,
                Action     = "moderate",
                ResultText = $"score={result.Score}, issues={string.Join("; ", result.Issues)}",
                IsSuccess  = true
            });

            if (result.Score > 70)
                article.Status = "Rejected";
            else if (result.Score >= 40)
                article.Status = "PendingReview";
            // score < 40: giữ nguyên trạng thái do journalist chọn (draft / PendingReview)
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Journalist ApplyAIModerationAsync failed (articleId={Id})", article.Id);
        }
    }

    private int? CurrentUserId => HttpContext.Session.GetInt32("JournalistId");
    private bool IsLoggedIn => CurrentUserId != null;

    // Kiểm tra quyền nhà báo
    private async Task<User?> GetCurrentJournalist()
    {
        if (CurrentUserId == null) return null;
        var user = await _db.Users.FindAsync(CurrentUserId.Value);
        if (user == null || user.Role != "Journalist") return null;
        return user;
    }

    // ===== REGISTER =====
    [HttpGet]
    public IActionResult Register()
    {
        if (IsLoggedIn) return RedirectToAction("Dashboard");
        return View(new JournalistRegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(JournalistRegisterViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(vm.Username) ||
            string.IsNullOrWhiteSpace(vm.Email) ||
            string.IsNullOrWhiteSpace(vm.Password) ||
            string.IsNullOrWhiteSpace(vm.FullName))
        {
            vm.Error = "Vui lòng điền đầy đủ thông tin.";
            return View(vm);
        }

        if (vm.Password.Length < 6)
        {
            vm.Error = "Mật khẩu phải có ít nhất 6 ký tự.";
            return View(vm);
        }

        if (vm.Password != vm.ConfirmPassword)
        {
            vm.Error = "Mật khẩu xác nhận không khớp.";
            return View(vm);
        }

        try
        {
            var lowerUsername = vm.Username.Trim().ToLowerInvariant();
            var lowerEmail = vm.Email.Trim().ToLowerInvariant();

            if (await _db.Users.AnyAsync(u => u.Username == lowerUsername))
            {
                vm.Error = "Tên đăng nhập đã tồn tại.";
                return View(vm);
            }
            if (await _db.Users.AnyAsync(u => u.Email == lowerEmail))
            {
                vm.Error = "Email đã được sử dụng.";
                return View(vm);
            }

            var user = new User
            {
                Username = lowerUsername,
                Email = lowerEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.Password),
                FullName = vm.FullName.Trim(),
                Bio = vm.Bio?.Trim(),
                Role = "Journalist",
                IsActive = false, // chờ admin/quản lý duyệt
                IsEmailConfirmed = false,
                CreatedAt = DateTime.Now
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // KHÔNG auto login — chờ duyệt
            TempData["PendingMsg"] = "Đăng ký nhà báo thành công! Tài khoản đang chờ quản trị viên/quản lý duyệt.";
            return RedirectToAction("Login");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Journalist Register failed");
            vm.Error = "Lỗi hệ thống. Vui lòng thử lại.";
            return View(vm);
        }
    }

    // ===== LOGIN =====
    [HttpGet]
    public IActionResult Login()
    {
        if (IsLoggedIn) return RedirectToAction("Dashboard");
        return View(new JournalistLoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(JournalistLoginViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(vm.UsernameOrEmail) || string.IsNullOrWhiteSpace(vm.Password))
        {
            vm.Error = "Vui lòng nhập đầy đủ thông tin.";
            return View(vm);
        }

        try
        {
            var key = vm.UsernameOrEmail.Trim().ToLowerInvariant();
            var user = await _db.Users.FirstOrDefaultAsync(u =>
                (u.Username == key || u.Email == key) && u.Role == "Journalist");

            if (user == null || !BCrypt.Net.BCrypt.Verify(vm.Password, user.PasswordHash))
            {
                vm.Error = "Sai tên đăng nhập / email hoặc mật khẩu.";
                return View(vm);
            }

            if (!user.IsActive)
            {
                vm.Error = "Tài khoản nhà báo đang chờ duyệt hoặc đã bị khoá.";
                return View(vm);
            }

            SetJournalistSession(user);
            return RedirectToAction("Dashboard");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Journalist Login failed");
            vm.Error = "Lỗi hệ thống. Vui lòng thử lại.";
            return View(vm);
        }
    }

    // ===== LOGOUT =====
    public IActionResult Logout()
    {
        HttpContext.Session.Remove("JournalistId");
        HttpContext.Session.Remove("JournalistName");
        HttpContext.Session.Remove("JournalistAvatar");
        return RedirectToAction("Login");
    }

    // ===== DASHBOARD =====
    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var user = await GetCurrentJournalist();
        if (user == null) return RedirectToAction("Login");

        var articles = await _db.Articles
            .Include(a => a.Category)
            .Include(a => a.Comments)
            .Where(a => a.AuthorUserId == user.Id)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var vm = new JournalistDashboardViewModel
        {
            User = user,
            Articles = articles,
            TotalViews = articles.Sum(a => a.Views),
            TotalComments = articles.Sum(a => a.Comments.Count),
            PublishedCount = articles.Count(a => a.Status == "published"),
            DraftCount = articles.Count(a => a.Status == "draft"),
            PendingCount = articles.Count(a => a.Status == "PendingReview" || a.Status == "PendingApproval")
        };

        return View(vm);
    }

    // ===== PROFILE =====
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await GetCurrentJournalist();
        if (user == null) return RedirectToAction("Login");

        var vm = new ProfileViewModel
        {
            User = user,
            Articles = await _db.Articles
                .Include(a => a.Category)
                .Where(a => a.AuthorUserId == user.Id)
                .OrderByDescending(a => a.CreatedAt)
                .Take(20)
                .ToListAsync(),
            CommentCount = await _db.Comments.CountAsync(c => c.UserId == user.Id)
        };

        return View(vm);
    }

    // ===== UPDATE PROFILE =====
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(string fullName, string email, string? bio)
    {
        var user = await GetCurrentJournalist();
        if (user == null) return Json(new { success = false, message = "Chưa đăng nhập" });

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
            return Json(new { success = false, message = "Vui lòng điền đầy đủ thông tin" });

        try
        {
            var lowerEmail = email.Trim().ToLowerInvariant();
            if (await _db.Users.AnyAsync(u => u.Email == lowerEmail && u.Id != user.Id))
                return Json(new { success = false, message = "Email đã được sử dụng" });

            user.FullName = fullName.Trim();
            user.Email = lowerEmail;
            user.Bio = bio?.Trim();
            await _db.SaveChangesAsync();

            HttpContext.Session.SetString("JournalistName", user.FullName);
            return Json(new { success = true, message = "Cập nhật thành công!" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UpdateProfile failed");
            return Json(new { success = false, message = "Lỗi hệ thống" });
        }
    }

    // ===== CHANGE PASSWORD =====
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        var user = await GetCurrentJournalist();
        if (user == null) return Json(new { success = false, message = "Chưa đăng nhập" });

        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            return Json(new { success = false, message = "Vui lòng điền đầy đủ" });

        if (newPassword.Length < 6)
            return Json(new { success = false, message = "Mật khẩu mới phải ít nhất 6 ký tự" });

        if (newPassword != confirmPassword)
            return Json(new { success = false, message = "Mật khẩu xác nhận không khớp" });

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return Json(new { success = false, message = "Mật khẩu hiện tại không đúng" });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        return Json(new { success = true, message = "Đổi mật khẩu thành công!" });
    }

    // ===== UPLOAD AVATAR =====
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAvatar(IFormFile avatarFile)
    {
        var user = await GetCurrentJournalist();
        if (user == null) return Json(new { success = false, message = "Chưa đăng nhập" });

        if (avatarFile == null || avatarFile.Length == 0)
            return Json(new { success = false, message = "Vui lòng chọn ảnh" });

        if (avatarFile.Length > 5 * 1024 * 1024)
            return Json(new { success = false, message = "Ảnh quá lớn (tối đa 5MB)" });

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(avatarFile.ContentType.ToLower()))
            return Json(new { success = false, message = "Chỉ hỗ trợ JPG, PNG, GIF, WEBP" });

        try
        {
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
            Directory.CreateDirectory(uploadsDir);

            var ext = Path.GetExtension(avatarFile.FileName).ToLower();
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            var fileName = $"journalist_{user.Id}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            // Xóa avatar cũ
            if (!string.IsNullOrEmpty(user.AvatarUrl) && user.AvatarUrl.StartsWith("/uploads/"))
            {
                var oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.AvatarUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
            }

            using (var stream = new FileStream(filePath, FileMode.Create))
                await avatarFile.CopyToAsync(stream);

            var avatarUrl = $"/uploads/avatars/{fileName}";
            user.AvatarUrl = avatarUrl;
            await _db.SaveChangesAsync();

            HttpContext.Session.SetString("JournalistAvatar", avatarUrl);
            return Json(new { success = true, message = "Cập nhật ảnh đại diện thành công!", avatarUrl });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UploadAvatar failed");
            return Json(new { success = false, message = "Lỗi hệ thống" });
        }
    }

    // ===== CREATE ARTICLE =====
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var user = await GetCurrentJournalist();
        if (user == null) return RedirectToAction("Login");

        var vm = new ArticleFormViewModel
        {
            Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ArticleFormViewModel vm, IFormFile? thumbnailFile)
    {
        var user = await GetCurrentJournalist();
        if (user == null) return RedirectToAction("Login");

        vm.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();

        if (string.IsNullOrWhiteSpace(vm.Article.Title) ||
            string.IsNullOrWhiteSpace(vm.Article.Summary) ||
            string.IsNullOrWhiteSpace(vm.Article.Content))
        {
            vm.Error = "Vui lòng điền đầy đủ tiêu đề, tóm tắt và nội dung.";
            return View(vm);
        }

        try
        {
            // Upload thumbnail
            if (thumbnailFile != null && thumbnailFile.Length > 0)
            {
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "articles");
                Directory.CreateDirectory(uploadsDir);
                var ext = Path.GetExtension(thumbnailFile.FileName).ToLower();
                var fileName = $"thumb_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadsDir, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await thumbnailFile.CopyToAsync(stream);
                vm.Article.Thumbnail = $"/uploads/articles/{fileName}";
            }

            // Generate slug
            var slug = SlugHelper.MakeSlug(vm.Article.Title);
            if (await _db.Articles.AnyAsync(a => a.Slug == slug))
                slug += "-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            vm.Article.Slug = slug;
            vm.Article.AuthorUserId = user.Id;
            vm.Article.Status = vm.Article.Status == "draft" ? "draft" : "PendingReview";
            vm.Article.CreatedAt = DateTime.Now;
            vm.Article.UpdatedAt = DateTime.Now;

            _db.Articles.Add(vm.Article);
            await _db.SaveChangesAsync();

            // AI moderation sau khi đã có Id
            await ApplyAIModerationAsync(vm.Article);
            await _db.SaveChangesAsync();

            // Save tags
            if (!string.IsNullOrWhiteSpace(vm.Tags))
            {
                var tagNames = vm.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim().ToLower()).Distinct();
                foreach (var name in tagNames)
                {
                    var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Name == name);
                    if (tag == null)
                    {
                        tag = new Tag { Name = name, Slug = SlugHelper.MakeSlug(name) };
                        _db.Tags.Add(tag);
                        await _db.SaveChangesAsync();
                    }
                    _db.ArticleTags.Add(new ArticleTag { ArticleId = vm.Article.Id, TagId = tag.Id });
                }
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "Bài viết đã được tạo thành công!";
            return RedirectToAction("Dashboard");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Journalist Create article failed");
            vm.Error = "Lỗi hệ thống. Thử lại sau.";
            return View(vm);
        }
    }

    // ===== EDIT ARTICLE =====
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await GetCurrentJournalist();
        if (user == null) return RedirectToAction("Login");

        var article = await _db.Articles
            .Include(a => a.ArticleTags).ThenInclude(at => at.Tag)
            .FirstOrDefaultAsync(a => a.Id == id && a.AuthorUserId == user.Id);

        if (article == null) return NotFound();

        var vm = new ArticleFormViewModel
        {
            Article = article,
            Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync(),
            Tags = string.Join(", ", article.ArticleTags.Select(at => at.Tag?.Name ?? ""))
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ArticleFormViewModel vm, IFormFile? thumbnailFile)
    {
        var user = await GetCurrentJournalist();
        if (user == null) return RedirectToAction("Login");

        var article = await _db.Articles
            .Include(a => a.ArticleTags)
            .FirstOrDefaultAsync(a => a.Id == id && a.AuthorUserId == user.Id);

        if (article == null) return NotFound();

        vm.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();

        if (string.IsNullOrWhiteSpace(vm.Article.Title) ||
            string.IsNullOrWhiteSpace(vm.Article.Summary) ||
            string.IsNullOrWhiteSpace(vm.Article.Content))
        {
            vm.Error = "Vui lòng điền đầy đủ tiêu đề, tóm tắt và nội dung.";
            vm.Article = article;
            return View(vm);
        }

        try
        {
            if (thumbnailFile != null && thumbnailFile.Length > 0)
            {
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "articles");
                Directory.CreateDirectory(uploadsDir);
                var ext = Path.GetExtension(thumbnailFile.FileName).ToLower();
                var fileName = $"thumb_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadsDir, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                    await thumbnailFile.CopyToAsync(stream);
                article.Thumbnail = $"/uploads/articles/{fileName}";
            }

            article.Title = vm.Article.Title;
            article.Summary = vm.Article.Summary;
            article.Content = vm.Article.Content;
            article.CategoryId = vm.Article.CategoryId;
            article.ThumbnailAlt = vm.Article.ThumbnailAlt;
            article.Region = vm.Article.Region;
            article.Status = vm.Article.Status == "draft" ? "draft" : "PendingReview";
            article.UpdatedAt = DateTime.Now;

            // AI moderation cập nhật Status nếu nội dung vi phạm
            await ApplyAIModerationAsync(article);

            var newSlug = SlugHelper.MakeSlug(article.Title);
            if (newSlug != article.Slug)
            {
                if (await _db.Articles.AnyAsync(a => a.Slug == newSlug && a.Id != article.Id))
                    newSlug += "-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                article.Slug = newSlug;
            }

            // Update tags
            _db.ArticleTags.RemoveRange(article.ArticleTags);
            if (!string.IsNullOrWhiteSpace(vm.Tags))
            {
                var tagNames = vm.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim().ToLower()).Distinct();
                foreach (var name in tagNames)
                {
                    var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Name == name);
                    if (tag == null)
                    {
                        tag = new Tag { Name = name, Slug = SlugHelper.MakeSlug(name) };
                        _db.Tags.Add(tag);
                        await _db.SaveChangesAsync();
                    }
                    _db.ArticleTags.Add(new ArticleTag { ArticleId = article.Id, TagId = tag.Id });
                }
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Cập nhật bài viết thành công!";
            return RedirectToAction("Dashboard");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Journalist Edit article failed");
            vm.Error = "Lỗi hệ thống. Thử lại sau.";
            vm.Article = article;
            return View(vm);
        }
    }

    // ===== DELETE ARTICLE =====
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await GetCurrentJournalist();
        if (user == null) return Json(new { success = false, message = "Chưa đăng nhập" });

        var article = await _db.Articles
            .Include(a => a.ArticleTags)
            .Include(a => a.Comments)
            .FirstOrDefaultAsync(a => a.Id == id && a.AuthorUserId == user.Id);

        if (article == null)
            return Json(new { success = false, message = "Không tìm thấy bài viết" });

        try
        {
            _db.ArticleTags.RemoveRange(article.ArticleTags);
            _db.Comments.RemoveRange(article.Comments);
            _db.Articles.Remove(article);
            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Đã xóa bài viết" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Journalist Delete article failed");
            return Json(new { success = false, message = "Lỗi hệ thống" });
        }
    }

    // ===== UPLOAD IMAGE (cho TinyMCE) =====
    [HttpPost]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        var user = await GetCurrentJournalist();
        if (user == null) return Json(new { success = false, message = "Unauthorized" });

        if (file == null || file.Length == 0)
            return Json(new { success = false, message = "No file" });

        if (file.Length > 5 * 1024 * 1024)
            return Json(new { success = false, message = "File too large" });

        try
        {
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "content");
            Directory.CreateDirectory(uploadsDir);
            var ext = Path.GetExtension(file.FileName).ToLower();
            var fileName = $"img_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            return Json(new { location = $"/uploads/content/{fileName}" });
        }
        catch
        {
            return Json(new { success = false, message = "Upload failed" });
        }
    }

    // ===== VIDEO (YouTube / Upload) =====
    public async Task<IActionResult> Videos()
    {
        var journalist = await GetCurrentJournalist();
        if (journalist == null) return RedirectToAction("Login");
        var list = await _db.Videos
            .Where(v => v.CreatedByUserId == journalist.Id)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
        return View(list);
    }

    public async Task<IActionResult> CreateVideo()
    {
        if (await GetCurrentJournalist() == null) return RedirectToAction("Login");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(VideoUploadService.MaxSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = VideoUploadService.MaxSize)]
    public async Task<IActionResult> CreateVideo(
        string videoType,
        string? youtubeUrl,
        string title,
        string? source,
        string? description,
        string status = "published",
        IFormFile? videoFile = null)
    {
        var journalist = await GetCurrentJournalist();
        if (journalist == null) return RedirectToAction("Login");

        if (string.IsNullOrWhiteSpace(title))
        {
            SetCreateVideoViewBag(videoType, youtubeUrl, title, source, description, "Vui lòng nhập tiêu đề.");
            return View();
        }

        var isUpload = videoType == "upload";
        Video video;

        if (isUpload)
        {
            if (videoFile == null || videoFile.Length == 0)
            {
                SetCreateVideoViewBag(videoType, youtubeUrl, title, source, description, "Vui lòng chọn file video.");
                return View();
            }
            var upload = await _videoUpload.SaveAsync(videoFile);
            if (!upload.Success)
            {
                SetCreateVideoViewBag(videoType, youtubeUrl, title, source, description, upload.Error ?? "Lỗi upload video.");
                return View();
            }
            video = new Video
            {
                Title = title.Trim(),
                YouTubeId = "",
                VideoType = "upload",
                VideoUrl = upload.RelativePath,
                FileSize = upload.FileSize,
                Source = string.IsNullOrWhiteSpace(source) ? journalist.FullName : source.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Status = status == "draft" ? "draft" : "published",
                CreatedByUserId = journalist.Id,
                CreatedAt = DateTime.Now,
                PublishedAt = DateTime.Now
            };
        }
        else
        {
            var vid = YouTubeHelper.ExtractId(youtubeUrl);
            if (string.IsNullOrEmpty(vid))
            {
                SetCreateVideoViewBag(videoType, youtubeUrl, title, source, description, "Vui lòng nhập link YouTube hợp lệ.");
                return View();
            }
            video = new Video
            {
                Title = title.Trim(),
                YouTubeId = vid,
                VideoType = "youtube",
                Source = string.IsNullOrWhiteSpace(source) ? journalist.FullName : source.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Status = status == "draft" ? "draft" : "published",
                CreatedByUserId = journalist.Id,
                CreatedAt = DateTime.Now,
                PublishedAt = DateTime.Now
            };
        }

        _db.Videos.Add(video);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã đăng video.";
        return RedirectToAction("Videos");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteVideo(int id)
    {
        var journalist = await GetCurrentJournalist();
        if (journalist == null) return Json(new { success = false });
        var v = await _db.Videos.FirstOrDefaultAsync(x => x.Id == id && x.CreatedByUserId == journalist.Id);
        if (v == null) return Json(new { success = false });
        if (v.VideoType == "upload") _videoUpload.DeletePhysicalFile(v.VideoUrl);
        _db.Videos.Remove(v);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    private void SetCreateVideoViewBag(string videoType, string? youtubeUrl, string? title, string? source, string? description, string error)
    {
        ViewBag.Error = error;
        ViewBag.VideoType = videoType;
        ViewBag.YoutubeUrl = youtubeUrl;
        ViewBag.VTitle = title;
        ViewBag.Source = source;
        ViewBag.Description = description;
    }

    // ===== HELPERS =====
    private void SetJournalistSession(User user)
    {
        HttpContext.Session.SetInt32("JournalistId", user.Id);
        HttpContext.Session.SetString("JournalistName", user.FullName);
        HttpContext.Session.SetString("JournalistAvatar", user.AvatarUrl ?? "");
    }

    // ===== HỘP THƯ NHÀ BÁO =====
    [HttpGet]
    public async Task<IActionResult> Inbox(
        string? keyword, string? type,
        string? status, DateTime? fromDate,
        DateTime? toDate, int page = 1)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");

        var userId = CurrentUserId;
        var email = HttpContext.Session.GetString("JournalistEmail") ?? "";
        const int pageSize = 15;

        var query = _db.Notifications.Where(n =>
            !n.IsDeleted &&
            (
                n.TargetType == "all" ||
                (n.TargetType == "user" && n.TargetUserId == userId) ||
                n.TargetType == "journalist" ||
                (n.TargetType == "email" && n.TargetEmail == email)
            )
        ).AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(n =>
                n.Title.Contains(keyword) ||
                n.Content.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(n => n.Type == type);
        if (status == "unread")
            query = query.Where(n => !n.IsRead);
        else if (status == "read")
            query = query.Where(n => n.IsRead);
        if (fromDate.HasValue)
            query = query.Where(n => n.CreatedAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(n => n.CreatedAt < toDate.Value.AddDays(1));

        var total = await query.CountAsync();
        var unreadCount = await _db.Notifications.CountAsync(n =>
            !n.IsRead && !n.IsDeleted &&
            (
                n.TargetType == "all" ||
                (n.TargetType == "user" && n.TargetUserId == userId) ||
                n.TargetType == "journalist" ||
                (n.TargetType == "email" && n.TargetEmail == email)
            ));

        var list = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Keyword = keyword;
        ViewBag.Type = type;
        ViewBag.Status = status;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
        ViewBag.TotalCount = total;
        ViewBag.UnreadCount = unreadCount;
        ViewBag.ReadCount = total - unreadCount;
        return View(list);
    }

    // Đánh dấu đã đọc
    [HttpPost]
    public async Task<IActionResult> MarkNotificationRead(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var n = await _db.Notifications.FindAsync(id);
        if (n == null) return Json(new { success = false });
        n.IsRead = true;
        n.ReadAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Đánh dấu tất cả đã đọc
    [HttpPost]
    public async Task<IActionResult> MarkAllNotificationsRead()
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var userId = CurrentUserId;
        var email = HttpContext.Session.GetString("JournalistEmail") ?? "";

        var list = await _db.Notifications.Where(n =>
            !n.IsRead && !n.IsDeleted &&
            (
                n.TargetType == "all" ||
                (n.TargetType == "user" && n.TargetUserId == userId) ||
                n.TargetType == "journalist" ||
                (n.TargetType == "email" && n.TargetEmail == email)
            )).ToListAsync();

        foreach (var n in list) { n.IsRead = true; n.ReadAt = DateTime.Now; }
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Xóa 1 thông báo
    [HttpPost]
    public async Task<IActionResult> DeleteNotification(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var n = await _db.Notifications.FindAsync(id);
        if (n == null) return Json(new { success = false });
        n.IsDeleted = true;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Xóa hàng loạt
    [HttpPost]
    public async Task<IActionResult> DeleteNotifications([FromBody] List<int> ids)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var list = await _db.Notifications
            .Where(n => ids.Contains(n.Id))
            .ToListAsync();
        foreach (var n in list) n.IsDeleted = true;
        await _db.SaveChangesAsync();
        return Json(new { success = true, deleted = list.Count });
    }

    // Xóa tất cả đã đọc
    [HttpPost]
    public async Task<IActionResult> DeleteAllReadNotifications()
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var userId = CurrentUserId;
        var email = HttpContext.Session.GetString("JournalistEmail") ?? "";

        var list = await _db.Notifications.Where(n =>
            n.IsRead && !n.IsDeleted &&
            (
                n.TargetType == "all" ||
                (n.TargetType == "user" && n.TargetUserId == userId) ||
                n.TargetType == "journalist" ||
                (n.TargetType == "email" && n.TargetEmail == email)
            )).ToListAsync();

        foreach (var n in list) n.IsDeleted = true;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Lấy số chưa đọc cho badge
    [HttpGet]
    public async Task<IActionResult> GetUnreadCount()
    {
        if (!IsLoggedIn) return Json(new { count = 0 });
        var userId = CurrentUserId;
        var email = HttpContext.Session.GetString("JournalistEmail") ?? "";

        var count = await _db.Notifications.CountAsync(n =>
            !n.IsRead && !n.IsDeleted &&
            (
                n.TargetType == "all" ||
                (n.TargetType == "user" && n.TargetUserId == userId) ||
                n.TargetType == "journalist" ||
                (n.TargetType == "email" && n.TargetEmail == email)
            ));
        return Json(new { count });
    }
}