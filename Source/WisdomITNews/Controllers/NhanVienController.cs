using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using WisdomITNews.Data;
using WisdomITNews.Models;
using WisdomITNews.Services;

namespace WisdomITNews.Controllers;

// ========================== KHU NHÂN VIÊN ==========================
// Lớp riêng cho nhân viên (editor). MỌI đường link đều /nhan-vien/...
// KHÔNG dùng /admin. Quyền: viết/sửa/duyệt bài, bình luận, danh mục,
// góp ý, đối tác — KHÔNG có: xóa bài, người dùng, newsletter, quản lý nhân viên.
public class NhanVienController : Controller
{
    private readonly AppDbContext _db;
    private readonly ImageUploadService _imageUpload;
    private readonly AIService _ai;
    private readonly VideoUploadService _videoUpload;
    private readonly ILogger<NhanVienController> _logger;
    private readonly IServiceProvider _serviceProvider;

    public NhanVienController(AppDbContext db, ImageUploadService imageUpload, AIService ai, VideoUploadService videoUpload, ILogger<NhanVienController> logger, IServiceProvider serviceProvider)
    {
        _db = db;
        _imageUpload = imageUpload;
        _ai = ai;
        _videoUpload = videoUpload;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    private bool IsLoggedIn => HttpContext.Session.GetString("AdminId") != null;
    private int AdminId => int.Parse(HttpContext.Session.GetString("AdminId") ?? "0");

    // Ghi nhật ký hoạt động (GĐ3) — BEST-EFFORT, lỗi log không làm hỏng thao tác chính. Gọi SAU SaveChanges.
    private async Task TryLogStaffAsync(string action, string detail, int? targetId = null)
    {
        try
        {
            _db.StaffActivityLogs.Add(new StaffActivityLog
            {
                ActorAdminId = int.TryParse(HttpContext.Session.GetString("AdminId"), out var aid) ? aid : (int?)null,
                ActorName = HttpContext.Session.GetString("AdminName") ?? "Hệ thống",
                ActorRole = HttpContext.Session.GetString("AdminRole") ?? "",
                Action = action,
                Detail = detail,
                TargetAdminId = targetId,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TryLogStaffAsync failed — có thể bảng StaffActivityLogs chưa được tạo (Rebuild + F5).");
        }
    }

    // ===== AUTH =====
    public IActionResult Login() => IsLoggedIn ? RedirectToAction("Dashboard") : View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password)
    {
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.Username == username && a.IsActive);
        if (admin == null || !BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash))
        {
            ViewBag.Error = "Sai tên đăng nhập hoặc mật khẩu!";
            return View();
        }
        admin.LastLogin = DateTime.Now;
        await _db.SaveChangesAsync();
        HttpContext.Session.SetString("AdminId", admin.Id.ToString());
        HttpContext.Session.SetString("AdminName", admin.FullName);
        HttpContext.Session.SetString("AdminRole", admin.Role);
        return RedirectToAction("Dashboard");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    // ===== DASHBOARD (teal) =====
    public async Task<IActionResult> Dashboard()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        ViewBag.TotalArticles   = await _db.Articles.CountAsync();
        ViewBag.DraftArticles   = await _db.Articles.CountAsync(a => a.Status == "draft");
        ViewBag.PendingComments = await _db.Comments.CountAsync(c => c.Status == "pending");
        ViewBag.OpenFeedbacks   = await _db.FeedbackReports.CountAsync(f => !f.IsResolved);
        ViewBag.PendingPartners = await _db.Users.CountAsync(u => u.Role == "Journalist" && !u.IsActive);
        return View();
    }

    // ===== ARTICLES =====
    public async Task<IActionResult> Articles(string status = "", int page = 1)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        const int pageSize = 20;
        var query = _db.Articles.Include(a => a.Category).Include(a => a.Author).Include(a => a.AuthorUser)
            .Where(a => a.IsExternal == false)  // CHỈ bài gốc của Wisdom
            .AsQueryable();
        if (!string.IsNullOrEmpty(status))
        {
            if (status == "PendingReview")
                query = query.Where(a => a.Status == "PendingReview" || a.Status == "PendingApproval");
            else
                query = query.Where(a => a.Status == status);
        }
        var total = await query.CountAsync();
        var arts = await query.OrderByDescending(a => a.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        ViewBag.Status = status;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
        return View(arts);
    }

    public async Task<IActionResult> CreateArticle()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        return View(new ArticleFormViewModel { Categories = await _db.Categories.ToListAsync() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateArticle(ArticleFormViewModel vm, IFormFile? thumbnailFile)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        vm.Categories = await _db.Categories.ToListAsync();

        if (string.IsNullOrWhiteSpace(vm.Article.Title) ||
            string.IsNullOrWhiteSpace(vm.Article.Summary) ||
            string.IsNullOrWhiteSpace(vm.Article.Content))
        {
            vm.Error = "Vui lòng điền đầy đủ tiêu đề, mô tả và nội dung!";
            return View(vm);
        }

        if (thumbnailFile != null && thumbnailFile.Length > 0)
        {
            var up = await _imageUpload.SaveAsync(thumbnailFile);
            if (!up.Success)
            {
                vm.Error = up.Error ?? "Lỗi upload ảnh";
                return View(vm);
            }
            vm.Article.Thumbnail = up.RelativePath;
        }

        var slug = SlugHelper.MakeSlug(vm.Article.Title);
        if (await _db.Articles.AnyAsync(a => a.Slug == slug))
            slug += "-" + DateTimeOffset.Now.ToUnixTimeSeconds();

        var article = new Article
        {
            Title = vm.Article.Title.Trim(),
            Slug = slug,
            Summary = vm.Article.Summary.Trim(),
            Content = vm.Article.Content,
            Thumbnail = vm.Article.Thumbnail?.Trim(),
            CategoryId = vm.Article.CategoryId,
            AuthorId = AdminId,
            Status = vm.Article.Status,
            IsFeatured = vm.Article.IsFeatured,
            IsBreaking = vm.Article.IsBreaking,
            Region = vm.Article.Region,
            PublishedAt = vm.Article.PublishedAt ?? (vm.Article.Status == "published" ? DateTime.Now : null),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _db.Articles.Add(article);
        await _db.SaveChangesAsync();
        await ApplyAIModerationAsync(article);
        await _db.SaveChangesAsync();
        await SaveTagsAsync(article.Id, vm.Tags);
        await TryLogStaffAsync("add_article", $"Nhân viên đăng bài: \"{article.Title}\"");
        return RedirectToAction("Articles");
    }

    public async Task<IActionResult> EditArticle(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var article = await _db.Articles.FindAsync(id);
        if (article == null) return NotFound();
        var tags = await _db.ArticleTags.Include(at => at.Tag).Where(at => at.ArticleId == id).Select(at => at.Tag!.Name).ToListAsync();
        return View(new ArticleFormViewModel
        {
            Article = article,
            Categories = await _db.Categories.ToListAsync(),
            Tags = string.Join(", ", tags)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditArticle(int id, ArticleFormViewModel vm, IFormFile? thumbnailFile)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var article = await _db.Articles.FindAsync(id);
        if (article == null) return NotFound();

        if (thumbnailFile != null && thumbnailFile.Length > 0)
        {
            var up = await _imageUpload.SaveAsync(thumbnailFile);
            if (!up.Success)
            {
                vm.Error = up.Error ?? "Lỗi upload ảnh";
                vm.Categories = await _db.Categories.ToListAsync();
                vm.Article = article;
                return View(vm);
            }
            article.Thumbnail = up.RelativePath;
        }
        else if (!string.IsNullOrWhiteSpace(vm.Article.Thumbnail))
        {
            article.Thumbnail = vm.Article.Thumbnail.Trim();
        }

        article.Title = vm.Article.Title.Trim();
        article.Summary = vm.Article.Summary.Trim();
        article.Content = vm.Article.Content;
        article.CategoryId = vm.Article.CategoryId;
        article.Status = vm.Article.Status;
        article.IsFeatured = vm.Article.IsFeatured;
        article.IsBreaking = vm.Article.IsBreaking;
        article.Region = vm.Article.Region;
        article.UpdatedAt = DateTime.Now;
        article.PublishedAt = vm.Article.PublishedAt ?? article.PublishedAt;
        if (article.Status == "published" && article.PublishedAt == null)
            article.PublishedAt = DateTime.Now;

        await ApplyAIModerationAsync(article);

        _db.ArticleTags.RemoveRange(_db.ArticleTags.Where(at => at.ArticleId == id));
        await _db.SaveChangesAsync();
        await SaveTagsAsync(id, vm.Tags);

        vm.Success = "Lưu thành công!";
        vm.Categories = await _db.Categories.ToListAsync();
        vm.Article = article;
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> ApproveArticle(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false, message = "Chưa đăng nhập" });
        var article = await _db.Articles.FindAsync(id);
        if (article == null) return Json(new { success = false, message = "Không tìm thấy bài viết" });
        article.Status = "published";
        if (!article.PublishedAt.HasValue) article.PublishedAt = DateTime.Now;
        article.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> RejectArticle(int id, [FromBody] RejectRequest? req)
    {
        if (!IsLoggedIn) return Json(new { success = false, message = "Chưa đăng nhập" });
        var article = await _db.Articles.FindAsync(id);
        if (article == null) return Json(new { success = false, message = "Không tìm thấy bài viết" });
        article.Status = "Rejected";
        article.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        if (article.AuthorUserId.HasValue)
        {
            var notifSvc = HttpContext.RequestServices.GetRequiredService<NotificationService>();
            await notifSvc.SendArticleRejectedAsync(
                article.AuthorUserId.Value,
                article.Id,
                article.Title,
                req?.Reason ?? "Không đáp ứng tiêu chuẩn nội dung"
            );
        }

        return Json(new { success = true });
    }

    public class RejectRequest { public string? Reason { get; set; } }

    // ===== COMMENTS =====
    public async Task<IActionResult> Comments(string status = "pending")
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var comments = await _db.Comments
            .Include(c => c.Article)
            .Where(c => status == "all" || c.Status == status)
            .OrderByDescending(c => c.CreatedAt)
            .Take(50).ToListAsync();
        ViewBag.Status = status;
        return View(comments);
    }

    [HttpPost]
    public async Task<IActionResult> ApproveComment(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var c = await _db.Comments.FindAsync(id);
        if (c == null) return Json(new { success = false });
        c.Status = "approved";
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteComment(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var c = await _db.Comments.FindAsync(id);
        if (c == null) return Json(new { success = false });
        _db.Comments.Remove(c);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ===== CATEGORIES (xem) =====
    public async Task<IActionResult> Categories()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        return View(await _db.Categories.OrderBy(c => c.SortOrder).ToListAsync());
    }

    // ===== FEEDBACKS =====
    public async Task<IActionResult> Feedbacks(string filter = "open")
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var query = _db.FeedbackReports.AsQueryable();
        if (filter == "open") query = query.Where(f => !f.IsResolved);
        else if (filter == "done") query = query.Where(f => f.IsResolved);
        var list = await query.OrderByDescending(f => f.CreatedAt).Take(100).ToListAsync();
        ViewBag.Filter = filter;
        return View(list);
    }

    [HttpPost]
    public async Task<IActionResult> ResolveFeedback(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var fb = await _db.FeedbackReports.FindAsync(id);
        if (fb == null) return Json(new { success = false });
        fb.IsResolved = true;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ===== ĐỐI TÁC (NHÀ BÁO) =====
    public async Task<IActionResult> Partners()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var journalists = await _db.Users
            .Where(u => u.Role == "Journalist")
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
        var ids = journalists.Select(u => u.Id).ToList();
        ViewBag.ArticleCounts = await _db.Articles
            .Where(a => a.AuthorUserId != null && ids.Contains(a.AuthorUserId.Value))
            .GroupBy(a => a.AuthorUserId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count);
        return View(journalists);
    }

    [HttpPost]
    public async Task<IActionResult> ApproveJournalist(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var u = await _db.Users.FindAsync(id);
        if (u == null || u.Role != "Journalist") return Json(new { success = false });
        u.IsActive = true;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> LockJournalist(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var u = await _db.Users.FindAsync(id);
        if (u == null || u.Role != "Journalist") return Json(new { success = false });
        u.IsActive = false;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

// ===== ĐĂNG VIDEO (YouTube / Upload) =====
    public async Task<IActionResult> Videos(string q = "")
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var query = _db.Videos.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var k = q.Trim().ToLower();
            query = query.Where(v => v.Title.ToLower().Contains(k) || (v.Source != null && v.Source.ToLower().Contains(k)));
        }
        var list = await query.OrderByDescending(v => v.CreatedAt).ToListAsync();
        ViewBag.Q = q;
        return View(list);
    }

    public IActionResult CreateVideo()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
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
        if (!IsLoggedIn) return RedirectToAction("Login");

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
                Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Status = status == "draft" ? "draft" : "published",
                CreatedByAdminId = AdminId,
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
                Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Status = status == "draft" ? "draft" : "published",
                CreatedByAdminId = AdminId,
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
        if (!IsLoggedIn) return Json(new { success = false });
        var v = await _db.Videos.FindAsync(id);
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
    }    // ===== XÓA BÀI VIẾT (nhân viên được phép) =====
    [HttpPost]
    public async Task<IActionResult> DeleteArticle(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var article = await _db.Articles.FindAsync(id);
        if (article == null) return Json(new { success = false });
        try
        {
            var logs = await _db.AILogs.Where(l => l.ArticleId == id).ToListAsync();
            foreach (var l in logs) l.ArticleId = null;
            var saved = await _db.SavedArticles.Where(x => x.ArticleId == id).ToListAsync();
            _db.SavedArticles.RemoveRange(saved);

            _db.Articles.Remove(article);
            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NhanVien DeleteArticle failed (id={Id})", id);
            return Json(new { success = false, message = "Không xóa được — bài còn dữ liệu liên quan." });
        }
    }

    // ===== QUẢN LÝ NGUỒN TIN =====

    // Danh sách nguồn + danh sách bài đã import (IsExternal = true)
    public async Task<IActionResult> RssSources(int? sourceId, string? keyword, DateTime? fromDate, DateTime? toDate, int page = 1)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        const int pageSize = 20;

        var sources = await _db.RssSources.OrderBy(s => s.Name).ToListAsync();

        var query = _db.Articles
            .Include(a => a.Category)
            .Where(a => a.IsExternal == true);

        if (sourceId.HasValue)
        {
            var src = sources.FirstOrDefault(s => s.Id == sourceId);
            if (src != null) query = query.Where(a => a.SourceName == src.Name);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(a => a.Title.Contains(keyword));

        if (fromDate.HasValue)
            query = query.Where(a => a.PublishedAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(a => a.PublishedAt < toDate.Value.AddDays(1));

        var total = await query.CountAsync();
        var articles = await query
            .OrderByDescending(a => a.PublishedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Sources = sources;
        ViewBag.SelectedSourceId = sourceId;
        ViewBag.Keyword = keyword;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
        return View(articles);
    }

    // Import từ 1 nguồn cụ thể
    [HttpPost]
    public async Task<IActionResult> ImportFromSource(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var source = await _db.RssSources.FindAsync(id);
        if (source == null) return Json(new { success = false, message = "Không tìm thấy nguồn" });
        if (!source.IsActive) return Json(new { success = false, message = "Nguồn này đang bị tắt" });

        var svc = HttpContext.RequestServices.GetRequiredService<NewsImportService>();
        var (added, updated, skipped) = await svc.ImportFromSourceAsync(source);
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = $"Đã nhập {added} bài mới, cập nhật {updated}, bỏ qua {skipped} từ {source.Name}" });
    }

    // Import tất cả nguồn đang active
    [HttpPost]
    public async Task<IActionResult> ImportAllSources()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var sources = await _db.RssSources.Where(s => s.IsActive).ToListAsync();
        var svc = HttpContext.RequestServices.GetRequiredService<NewsImportService>();

        int totalAdded = 0, totalUpdated = 0;
        foreach (var src in sources)
        {
            var (added, updated, _) = await svc.ImportFromSourceAsync(src);
            totalAdded += added; totalUpdated += updated;
        }
        await _db.SaveChangesAsync();
        TempData["Ok"] = $"Nhập xong tất cả nguồn: {totalAdded} bài mới, {totalUpdated} cập nhật.";
        return RedirectToAction("RssSources");
    }

    // Xóa bài đã import từ nguồn
    [HttpPost]
    public async Task<IActionResult> DeleteImportedArticle(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var article = await _db.Articles.FindAsync(id);
        if (article == null || !article.IsExternal) return Json(new { success = false });
        _db.Articles.Remove(article);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Xóa hàng loạt bài đã import
    [HttpPost]
    public async Task<IActionResult> DeleteImportedArticles([FromBody] List<int> ids)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (ids == null || ids.Count == 0)
            return Json(new { success = false, message = "Không có bài nào được chọn" });

        var articles = await _db.Articles
            .Where(a => ids.Contains(a.Id) && a.IsExternal == true)
            .ToListAsync();

        if (articles.Count == 0)
            return Json(new { success = false, message = "Không tìm thấy bài viết hợp lệ" });

        _db.Articles.RemoveRange(articles);
        await _db.SaveChangesAsync();
        return Json(new { success = true, deleted = articles.Count });
    }

    // ===== HELPERS =====
    private async Task ApplyAIModerationAsync(Article article)
    {
        try
        {
            var combined = $"{article.Title}\n\n{article.Summary}\n\n{article.Content}";
            var result = await _ai.ModerateContentAsync(combined);
            _db.AILogs.Add(new AILog
            {
                ArticleId = article.Id == 0 ? null : article.Id,
                Action = "moderate",
                ResultText = $"score={result.Score}, issues={string.Join("; ", result.Issues)}",
                IsSuccess = true
            });
            if (result.Score > 70) article.Status = "Rejected";
            else if (result.Score >= 40) article.Status = "PendingReview";
            else if (article.Status != "draft" && article.Status != "published") article.Status = "PendingApproval";

            if (article.AuthorUserId.HasValue && result.Score > 40)
            {
                var notifSvc = _serviceProvider.GetRequiredService<NotificationService>();
                await notifSvc.SendAiViolationAsync(
                    article.AuthorUserId.Value,
                    $"Nội dung bị gắn cờ: {string.Join(", ", result.Issues)}",
                    null
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ApplyAIModerationAsync failed (articleId={Id})", article.Id);
        }
    }

    private async Task SaveTagsAsync(int articleId, string tagsRaw)
    {
        if (string.IsNullOrWhiteSpace(tagsRaw)) return;
        var tagNames = tagsRaw.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t));
        foreach (var name in tagNames)
        {
            var slug = SlugHelper.MakeSlug(name);
            var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Slug == slug);
            if (tag == null)
            {
                tag = new Tag { Name = name, Slug = slug };
                _db.Tags.Add(tag);
                await _db.SaveChangesAsync();
            }
            if (!await _db.ArticleTags.AnyAsync(at => at.ArticleId == articleId && at.TagId == tag.Id))
                _db.ArticleTags.Add(new ArticleTag { ArticleId = articleId, TagId = tag.Id });
        }
        await _db.SaveChangesAsync();
    }

    // ===== QUẢN LÝ KHÁCH HÀNG =====
    [HttpGet]
    public async Task<IActionResult> Customers(string? filter = "all")
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var query = _db.NewsletterSubscribers.AsQueryable();
        if (filter == "active") query = query.Where(n => n.Status == "active");
        else if (filter == "inactive") query = query.Where(n => n.Status != "active");
        var subs = await query.OrderByDescending(n => n.SubscribedAt).ToListAsync();
        ViewBag.Filter = filter;
        return View(subs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSubscriber(int id, string? fullName, string? phone, string email, string status, string? source)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var s = await _db.NewsletterSubscribers.FindAsync(id);
        if (s != null)
        {
            if (!string.IsNullOrWhiteSpace(email)) s.Email = email.Trim();
            s.FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim();
            s.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
            s.Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
            s.Status = status == "active" ? "active" : "inactive";
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Đã cập nhật khách hàng.";
        }
        return RedirectToAction("Customers");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSubscriber(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var s = await _db.NewsletterSubscribers.FindAsync(id);
        if (s == null) return Json(new { success = false });
        _db.NewsletterSubscribers.Remove(s);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ===== QUẢN LÝ THÔNG BÁO =====

    public async Task<IActionResult> Notifications(
        string? keyword, string? type,
        DateTime? fromDate, DateTime? toDate, int page = 1)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        const int pageSize = 20;

        var query = _db.Notifications
            .Where(n => !n.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(n => n.Title.Contains(keyword) || n.Content.Contains(keyword));
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(n => n.Type == type);
        if (fromDate.HasValue)
            query = query.Where(n => n.CreatedAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(n => n.CreatedAt < toDate.Value.AddDays(1));

        var total = await query.CountAsync();
        var list = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Keyword = keyword;
        ViewBag.Type = type;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
        ViewBag.UnreadCount = await _db.Notifications.CountAsync(n => !n.IsRead && !n.IsDeleted);
        return View(list);
    }

    [HttpPost]
    public async Task<IActionResult> SendNotification(
        string title, string content, string targetType,
        string? targetEmail, string icon = "bell", string iconColor = "#159aa3")
    {
        if (!IsLoggedIn) return Json(new { success = false });

        // Nhân viên chỉ được gửi all hoặc journalist
        var allowedTargets = new[] { "all", "journalist" };
        if (!allowedTargets.Contains(targetType))
            return Json(new { success = false, message = "Bạn không có quyền gửi loại thông báo này" });

        var svc = HttpContext.RequestServices.GetRequiredService<NotificationService>();

        if (targetType == "all")
            await svc.SendSystemAsync(title, content, icon, iconColor);
        else if (targetType == "journalist")
            await svc.SendToJournalistsAsync(title, content);

        return Json(new { success = true, message = "Đã gửi thông báo thành công" });
    }

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

    [HttpGet]
    public async Task<IActionResult> GetUnreadCount()
    {
        if (!IsLoggedIn) return Json(new { count = 0 });
        var count = await _db.Notifications
            .CountAsync(n => !n.IsRead && !n.IsDeleted);
        return Json(new { count });
    }

    [HttpGet]
    public async Task<IActionResult> GetRecentNotifications()
    {
        if (!IsLoggedIn) return Json(new List<object>());
        var list = await _db.Notifications
            .Where(n => !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .Take(5)
            .Select(n => new {
                n.Id, n.Code, n.Title, n.Content,
                n.Type, n.Icon, n.IconColor,
                n.IsRead, n.ViolationContent, n.ViolationReason,
                CreatedAt = n.CreatedAt.ToString("dd/MM/yyyy HH:mm")
            })
            .ToListAsync();
        return Json(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAllReadNotifications()
    {
        if (!IsLoggedIn) return Json(new { success = false });

        var list = await _db.Notifications
            .Where(n => n.IsRead && !n.IsDeleted)
            .ToListAsync();

        foreach (var n in list) n.IsDeleted = true;
        await _db.SaveChangesAsync();
        return Json(new { success = true, deleted = list.Count });
    }

    // ===== LỊCH SỬ LÀM VIỆC (GĐ3) — nhân viên chỉ XEM, không sửa/xóa =====
    public async Task<IActionResult> ActivityLog(string? q, string? actionType)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var query = _db.StaffActivityLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(actionType)) query = query.Where(l => l.Action == actionType);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var kw = q.Trim();
            query = query.Where(l => l.Detail.Contains(kw) || l.ActorName.Contains(kw));
        }
        var logs = await query.OrderByDescending(l => l.CreatedAt).Take(500).ToListAsync();
        ViewBag.Q = q; ViewBag.ActionFilter = actionType;
        return View(logs);
    }

    // ===== PODCAST / AUDIO (của tôi) =====
    public async Task<IActionResult> Podcasts()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var list = await _db.Podcasts.Include(p => p.Article)
            .Where(p => p.UploadedByType == "NhanVien" && p.UploadedByUserId == AdminId)
            .OrderByDescending(p => p.CreatedAt).ToListAsync();
        return View(list);
    }

    // API JSON: tìm bài viết cho modal chọn bài (phân trang)
    public async Task<IActionResult> SearchArticlesForPicker(string? q, int page = 1, int pageSize = 8)
    {
        if (!IsLoggedIn) return Json(new { items = new object[0], hasMore = false });
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 30) pageSize = 8;

        var query = _db.Articles.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var kw = q.Trim();
            query = query.Where(a => a.Title.Contains(kw));
        }
        var raw = await query.OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(a => new { a.Id, a.Title, a.Thumbnail, a.Region, a.Views })
            .ToListAsync();

        var hasMore = raw.Count > pageSize;
        var items = raw.Take(pageSize).Select(a => new
        {
            id = a.Id,
            title = a.Title,
            thumbnail = a.Thumbnail,
            region = WisdomITNews.Services.SlugHelper.RegionName(a.Region),
            views = a.Views
        });
        return Json(new { items, hasMore });
    }


    [HttpPost]
    public async Task<IActionResult> UploadPodcast(IFormFile audioFile, string? title, string? description, int? articleId)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var svc = HttpContext.RequestServices.GetRequiredService<PodcastService>();
        var res = await svc.SaveUploadAsync(audioFile, title, description, articleId, AdminId, "NhanVien");
        TempData[res.Success ? "Ok" : "Err"] = res.Success ? "Đã tải lên audio." : ("Lỗi: " + res.Error);
        return RedirectToAction("Podcasts");
    }

    [HttpPost]
    public async Task<IActionResult> DeletePodcast(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var pod = await _db.Podcasts.FindAsync(id);
        if (pod == null || pod.UploadedByType != "NhanVien" || pod.UploadedByUserId != AdminId) return Json(new { success = false });
        _db.Podcasts.Remove(pod);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> GeneratePodcast(int articleId)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var svc = HttpContext.RequestServices.GetRequiredService<PodcastService>();
        var res = await svc.GenerateFromArticleAsync(articleId, AdminId, "NhanVien");
        return Json(new { success = res.Success, message = res.Success ? "Đã tạo audio từ bài viết." : res.Error, filePath = res.Podcast?.FilePath });
    }
// ===== GIA HẠN QUẢNG CÁO (chat với nhà báo) =====
    public async Task<IActionResult> RenewalAds()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var now = DateTime.Now;
        var ads = await _db.Advertisements
            .Where(a => a.EndDate != null && a.EndDate < now)
            .OrderByDescending(a => a.EndDate).ToListAsync();
        var ids = ads.Select(a => a.Id).ToList();
        var unread = await _db.AdRenewalMessages
            .Where(m => ids.Contains(m.AdvertisementId) && m.SenderRole == "journalist" && !m.IsReadByAdmin)
            .GroupBy(m => m.AdvertisementId).Select(g => new { AdId = g.Key, C = g.Count() }).ToListAsync();
        ViewBag.Unread = unread.ToDictionary(x => x.AdId, x => x.C);
        return View(ads);
    }

    public async Task<IActionResult> AdChat(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var ad = await _db.Advertisements.FindAsync(id);
        if (ad == null) return RedirectToAction("RenewalAds");
        var msgs = await _db.AdRenewalMessages.Where(m => m.AdvertisementId == id).OrderBy(m => m.Id).ToListAsync();
        var unread = msgs.Where(m => m.SenderRole == "journalist" && !m.IsReadByAdmin).ToList();
        if (unread.Count > 0) { foreach (var m in unread) m.IsReadByAdmin = true; await _db.SaveChangesAsync(); }
        ViewBag.Ad = ad; ViewBag.Messages = msgs;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ExtendAdRenewal(int id, DateTime? endDate)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var ad = await _db.Advertisements.FindAsync(id);
        if (ad == null) { TempData["Err"] = "Không tìm thấy quảng cáo."; return RedirectToAction("RenewalAds"); }
        if (endDate == null || endDate.Value.Date <= DateTime.Now.Date)
        { TempData["Err"] = "Chọn ngày gia hạn trong tương lai."; return RedirectToAction("AdChat", new { id }); }

        ad.EndDate = endDate.Value;
        if (ad.StartDate == null || ad.StartDate > DateTime.Now) ad.StartDate = DateTime.Now;
        ad.IsActive = true;
        ad.Status = "approved";

        var aRole = HttpContext.Session.GetString("AdminRole") ?? "";
        var role = (aRole == "superadmin" || aRole == "admin") ? "admin" : "nhanvien";
        var name = HttpContext.Session.GetString("AdminName") ?? "Quản trị";
        var msg = new AdRenewalMessage
        {
            AdvertisementId = id, SenderRole = role, SenderId = AdminId, SenderName = name,
            Content = $"✅ Đã gia hạn quảng cáo, chạy lại đến {endDate.Value:dd/MM/yyyy}.",
            CreatedAt = DateTime.Now, IsReadByAdmin = true, IsReadByJournalist = false
        };
        _db.AdRenewalMessages.Add(msg);
        await _db.SaveChangesAsync();

        var hub = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<WisdomITNews.Hubs.AdChatHub>>();
        await hub.Clients.Group($"adchat_{id}").SendAsync("ReceiveAdMessage", new
        {
            adId = id, id = msg.Id, role = msg.SenderRole, senderName = msg.SenderName,
            content = msg.Content, createdAt = msg.CreatedAt.ToString("HH:mm dd/MM/yyyy"), isJournalist = false
        });
        TempData["Ok"] = $"Đã gia hạn quảng cáo đến {endDate.Value:dd/MM/yyyy} và bật lại.";
        return RedirectToAction("AdChat", new { id });
    }

    // ===== QUẢNG CÁO (Nhân viên quản lý + duyệt) =====
    public async Task<IActionResult> Advertisements(string? filter)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var query = _db.Advertisements.AsQueryable();
        if (filter == "pending") query = query.Where(a => a.Status == "pending");
        else if (filter == "active") query = query.Where(a => a.Status == "approved" && a.IsActive);
        else if (filter == "header" || filter == "sidebar" || filter == "in_article") query = query.Where(a => a.Position == filter);
        var ads = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
        ViewBag.Filter = filter ?? "all";
        ViewBag.PendingCount = await _db.Advertisements.CountAsync(a => a.Status == "pending");
        ViewBag.TotalImpressions = (await _db.Advertisements.SumAsync(a => (int?)a.Impressions)) ?? 0;
        ViewBag.TotalClicks = (await _db.Advertisements.SumAsync(a => (int?)a.Clicks)) ?? 0;
        return View(ads);
    }

    public IActionResult CreateAd()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        return View(new Advertisement());
    }

    [HttpPost]
    public async Task<IActionResult> CreateAd(Advertisement form, IFormFile? imageFile)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (string.IsNullOrWhiteSpace(form.Title) || string.IsNullOrWhiteSpace(form.TargetUrl))
        { ViewBag.Error = "Vui lòng nhập tiêu đề và link đích."; return View(form); }
        string? img = form.ImageUrl;
        if (imageFile != null && imageFile.Length > 0)
        { var up = await _imageUpload.SaveAsync(imageFile); if (up.Success) img = up.RelativePath; }
        var validPos = new[] { "header", "sidebar", "in_article" };
        _db.Advertisements.Add(new Advertisement
        {
            Title = form.Title.Trim(), ImageUrl = img, TargetUrl = form.TargetUrl.Trim(),
            Position = validPos.Contains(form.Position) ? form.Position : "sidebar",
            StartDate = form.StartDate, EndDate = form.EndDate, IsActive = form.IsActive,
            Status = "approved", CreatedByAdminId = AdminId,
            CreatedByName = HttpContext.Session.GetString("AdminName") ?? "Nhân viên", CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();
        await TryLogStaffAsync("ad_create", "Tạo quảng cáo: " + form.Title.Trim());
        TempData["Ok"] = "Đã thêm quảng cáo.";
        return RedirectToAction("Advertisements");
    }

    public async Task<IActionResult> EditAd(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var ad = await _db.Advertisements.FindAsync(id);
        if (ad == null) return RedirectToAction("Advertisements");
        return View(ad);
    }

    [HttpPost]
    public async Task<IActionResult> EditAd(int id, Advertisement form, IFormFile? imageFile)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var ad = await _db.Advertisements.FindAsync(id);
        if (ad == null) return RedirectToAction("Advertisements");
        if (imageFile != null && imageFile.Length > 0)
        { var up = await _imageUpload.SaveAsync(imageFile); if (up.Success) ad.ImageUrl = up.RelativePath; }
        else if (!string.IsNullOrWhiteSpace(form.ImageUrl)) ad.ImageUrl = form.ImageUrl.Trim();
        ad.Title = (form.Title ?? "").Trim();
        ad.TargetUrl = (form.TargetUrl ?? "").Trim();
        var validPos = new[] { "header", "sidebar", "in_article" };
        ad.Position = validPos.Contains(form.Position) ? form.Position : ad.Position;
        ad.StartDate = form.StartDate; ad.EndDate = form.EndDate; ad.IsActive = form.IsActive;
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã cập nhật quảng cáo.";
        return RedirectToAction("Advertisements");
    }

    [HttpPost]
    public async Task<IActionResult> ToggleAd(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var ad = await _db.Advertisements.FindAsync(id);
        if (ad == null) return Json(new { success = false });
        ad.IsActive = !ad.IsActive;
        await _db.SaveChangesAsync();
        return Json(new { success = true, isActive = ad.IsActive });
    }

    [HttpPost]
    public async Task<IActionResult> ApproveAd(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var ad = await _db.Advertisements.FindAsync(id);
        if (ad == null) return Json(new { success = false });
        ad.Status = "approved"; ad.IsActive = true;
        await _db.SaveChangesAsync();
        await TryLogStaffAsync("ad_approve", "Duyệt quảng cáo #" + id);
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> RejectAd(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var ad = await _db.Advertisements.FindAsync(id);
        if (ad == null) return Json(new { success = false });
        ad.Status = "rejected"; ad.IsActive = false;
        await _db.SaveChangesAsync();
        await TryLogStaffAsync("ad_reject", "Từ chối quảng cáo #" + id);
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteAd(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var ad = await _db.Advertisements.FindAsync(id);
        if (ad == null) return Json(new { success = false });
        _db.Advertisements.Remove(ad);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ===== CHAT NỘI BỘ BAN QUẢN LÝ (phòng chung) =====
    public async Task<IActionResult> TeamChat()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var msgs = await _db.TeamChatMessages.Where(m => m.ConversationKey == "team")
            .OrderBy(m => m.Id).Take(200).ToListAsync();
        ViewBag.Messages = msgs;
        var role = HttpContext.Session.GetString("AdminRole") ?? "";
        ViewBag.MeRole = (role == "superadmin" || role == "admin") ? "admin" : "nhanvien";
        ViewBag.MeId = AdminId;
        return View();
    }

    // ===== NHÂN VIÊN: QUẢN LÝ KHÁCH HÀNG (Premium / Trial) =====
    public async Task<IActionResult> PremiumCustomers(string? role, string? status, int? planId, string? q, bool? expiring, int page = 1)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        const int pageSize = 20;

        var all = await CustomerHelper.BuildAllAsync(_db);
        var filtered = CustomerHelper.ApplyFilter(all, role, status, planId, q);

        var now = DateTime.Now;
        if (expiring == true)
            filtered = filtered.Where(i => i.SubStatus == SubscriptionStatus.Active && i.EndDate > now && i.EndDate <= now.AddDays(7)).ToList();

        ViewBag.TotalCount    = filtered.Count;
        ViewBag.TrialCount    = filtered.Count(i => i.SubStatus == SubscriptionStatus.Trial);
        ViewBag.PremiumCount  = filtered.Count(i => i.SubStatus == SubscriptionStatus.Active);
        ViewBag.ExpiringCount = filtered.Count(i => i.SubStatus == SubscriptionStatus.Active && i.EndDate > now && i.EndDate <= now.AddDays(7));

        var ordered = filtered.OrderByDescending(i => i.EndDate).ToList();
        int total = ordered.Count;
        int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        if (page < 1) page = 1;
        if (page > totalPages) page = totalPages;
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        ViewBag.Page = page; ViewBag.TotalPages = totalPages;
        ViewBag.Role = role; ViewBag.Status = status; ViewBag.PlanId = planId; ViewBag.Q = q; ViewBag.Expiring = expiring;
        ViewBag.Plans = await _db.SubscriptionPlans.OrderBy(p => p.Name).ToListAsync();
        return View(pageItems);
    }

    // Chi tiết 1 khách hàng
    public async Task<IActionResult> CustomerProfile(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var vm = await CustomerHelper.BuildDetailAsync(_db, id);
        if (vm == null) { TempData["Err"] = "Không tìm thấy khách hàng Premium/Trial."; return RedirectToAction("PremiumCustomers"); }
        ViewBag.Plans = await _db.SubscriptionPlans.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleCustomerAccount(int userId)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var u = await _db.Users.FindAsync(userId);
        if (u == null || u.IsDeleted) { TempData["Err"] = "Không tìm thấy khách hàng."; return RedirectToAction("PremiumCustomers"); }
        bool wasActive = u.IsActive;
        u.IsActive = !u.IsActive;
        CustomerHelper.AddLog(_db, userId, "NhanVien", HttpContext.Session.GetString("AdminName") ?? "Nhân viên",
            u.IsActive ? "Mở khóa tài khoản" : "Khóa tài khoản",
            wasActive ? "Hoạt động" : "Bị khóa", u.IsActive ? "Hoạt động" : "Bị khóa", null);
        await _db.SaveChangesAsync();
        TempData["Ok"] = u.IsActive ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản.";
        return RedirectToAction("CustomerProfile", new { id = userId });
    }

    [HttpPost]
    public async Task<IActionResult> CancelCustomerSub(int subId, string? note)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var s = await _db.UserSubscriptions.FindAsync(subId);
        if (s == null) { TempData["Err"] = "Không tìm thấy gói."; return RedirectToAction("PremiumCustomers"); }
        var actor = HttpContext.Session.GetString("AdminName") ?? "Nhân viên";
        var oldLbl = CustomerHelper.StatusLabel(s.Status);
        s.Status = SubscriptionStatus.Cancelled;
        s.Notes = (string.IsNullOrEmpty(s.Notes) ? "" : s.Notes + " | ")
                + $"Hủy bởi {actor} ({DateTime.Now:dd/MM/yyyy HH:mm})"
                + (string.IsNullOrWhiteSpace(note) ? "" : ": " + note.Trim());
        CustomerHelper.AddLog(_db, s.UserId, "NhanVien", actor, "Hủy gói", oldLbl, "Đã hủy", note);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã hủy gói của khách hàng.";
        return RedirectToAction("CustomerProfile", new { id = s.UserId });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateCustomerPlan(int subId, int planId, DateTime endDate, string? note)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var s = await _db.UserSubscriptions.Include(x => x.Plan).FirstOrDefaultAsync(x => x.Id == subId);
        if (s == null) { TempData["Err"] = "Không tìm thấy gói."; return RedirectToAction("PremiumCustomers"); }
        var plan = await _db.SubscriptionPlans.FindAsync(planId);
        if (plan == null) { TempData["Err"] = "Gói không hợp lệ."; return RedirectToAction("CustomerProfile", new { id = s.UserId }); }
        var actor = HttpContext.Session.GetString("AdminName") ?? "Nhân viên";

        if (s.PlanId != planId)
        {
            CustomerHelper.AddLog(_db, s.UserId, "NhanVien", actor, "Đổi gói", s.Plan?.Name ?? "", plan.Name, note);
            s.PlanId = planId;
        }
        if (s.EndDate.Date != endDate.Date)
        {
            CustomerHelper.AddLog(_db, s.UserId, "NhanVien", actor, "Đổi ngày hết hạn",
                s.EndDate.ToString("dd/MM/yyyy"), endDate.ToString("dd/MM/yyyy"), note);
            s.EndDate = endDate;
        }
        if (endDate > DateTime.Now && (s.Status == SubscriptionStatus.Expired || s.Status == SubscriptionStatus.Cancelled))
        {
            s.Status = SubscriptionStatus.Active;
            s.ConfirmedAt ??= DateTime.Now;
        }
        s.Notes = (string.IsNullOrEmpty(s.Notes) ? "" : s.Notes + " | ")
                + $"Cập nhật gói bởi {actor} ({DateTime.Now:dd/MM/yyyy HH:mm})";
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã cập nhật gói khách hàng.";
        return RedirectToAction("CustomerProfile", new { id = s.UserId });
    }

    [HttpPost]
    public async Task<IActionResult> RegisterPremium(int userId, int planId, int days)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var (ok, msg, uid) = await CustomerHelper.RegisterPremiumAsync(_db, userId, planId, days, "NhanVien", HttpContext.Session.GetString("AdminName") ?? "Nhân viên");
        TempData[ok ? "Ok" : "Err"] = msg;
        return uid > 0 ? RedirectToAction("CustomerProfile", new { id = uid }) : RedirectToAction("PremiumCustomers");
    }

    [HttpPost]
    public async Task<IActionResult> ExtendPremium(int subId, int days)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var (ok, msg, uid) = await CustomerHelper.ExtendPremiumAsync(_db, subId, days, "NhanVien", HttpContext.Session.GetString("AdminName") ?? "Nhân viên");
        TempData[ok ? "Ok" : "Err"] = msg;
        return uid > 0 ? RedirectToAction("CustomerProfile", new { id = uid }) : RedirectToAction("PremiumCustomers");
    }

    [HttpPost]
    public async Task<IActionResult> ConvertTrialToPremium(int subId, int planId, int days)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var (ok, msg, uid) = await CustomerHelper.ConvertTrialAsync(_db, subId, planId, days, "NhanVien", HttpContext.Session.GetString("AdminName") ?? "Nhân viên");
        TempData[ok ? "Ok" : "Err"] = msg;
        return uid > 0 ? RedirectToAction("CustomerProfile", new { id = uid }) : RedirectToAction("PremiumCustomers");
    }
}
