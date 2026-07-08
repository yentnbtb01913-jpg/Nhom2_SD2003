using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        _db.Articles.Remove(article);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
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
}
