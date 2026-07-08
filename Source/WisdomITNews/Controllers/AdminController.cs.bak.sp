using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;
using WisdomITNews.Services;

namespace WisdomITNews.Controllers;

public class AdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly ImageUploadService _imageUpload;
    private readonly AIService _ai;
    private readonly EmailService _email;
    private readonly VideoUploadService _videoUpload;
    private readonly ILogger<AdminController> _logger;
    private readonly IServiceProvider _serviceProvider;

    public AdminController(
        AppDbContext db,
        ImageUploadService imageUpload,
        AIService ai,
        EmailService email,
        VideoUploadService videoUpload,
        ILogger<AdminController> logger,
        IServiceProvider serviceProvider)
    {
        _db = db;
        _imageUpload = imageUpload;
        _ai = ai;
        _email = email;
        _videoUpload = videoUpload;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    private bool IsLoggedIn => HttpContext.Session.GetString("AdminId") != null;
    private int AdminId => int.Parse(HttpContext.Session.GetString("AdminId") ?? "0");
    private bool IsSuperAdmin => HttpContext.Session.GetString("AdminRole") == "superadmin";

    // KHÓA khu /admin: chỉ Super Admin. Nhân viên (editor) bị đẩy sang khu /nhan-vien.
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        var action = (context.RouteData.Values["action"]?.ToString() ?? "").ToLowerInvariant();
        if (IsLoggedIn && !IsSuperAdmin && action != "login" && action != "logout")
        {
            context.Result = Redirect("/nhan-vien/Dashboard");
            return;
        }
        base.OnActionExecuting(context);
    }

    // ===== AUTH =====
    public IActionResult Login() => IsLoggedIn ? RedirectToAction("Dashboard") : View();

    [HttpPost]
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
        // superadmin → Dashboard admin; nhân viên (editor) → khu Wisdom Nhân viên
        return admin.Role == "superadmin"
            ? RedirectToAction("Dashboard")
            : RedirectToAction("Dashboard", "NhanVien");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    // ===== TRANG QUẢN LÝ (cổng riêng cho nhân viên / editor) =====
    // Là "nhà" của nhân viên: chỉ gồm các lối tắt tới chức năng họ được phép.
    // Editor đăng nhập sẽ vào thẳng đây; superadmin vẫn dùng Dashboard tổng quan.
    public async Task<IActionResult> Manage()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");

        ViewBag.TotalArticles   = await _db.Articles.CountAsync();
        ViewBag.DraftArticles   = await _db.Articles.CountAsync(a => a.Status == "draft");
        ViewBag.PendingComments = await _db.Comments.CountAsync(c => c.Status == "pending");
        ViewBag.OpenFeedbacks   = await _db.FeedbackReports.CountAsync(f => !f.IsResolved);
        ViewBag.PendingPartners = await _db.Users.CountAsync(u => u.Role == "Journalist" && !u.IsActive);
        return View();
    }

    // ===== DASHBOARD =====
    public async Task<IActionResult> Dashboard()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");

        var vm = new DashboardViewModel
        {
            TotalArticles = await _db.Articles.CountAsync(),
            PublishedArticles = await _db.Articles.CountAsync(a => a.Status == "published"),
            TotalViews = await _db.Articles.SumAsync(a => (int?)a.Views) ?? 0,
            TotalComments = await _db.Comments.CountAsync(),
            PendingComments = await _db.Comments.CountAsync(c => c.Status == "pending"),
            Subscribers = await _db.NewsletterSubscribers.CountAsync(n => n.Status == "active"),
            RecentArticles = await _db.Articles.Include(a => a.Category).Include(a => a.Author)
                                .OrderByDescending(a => a.CreatedAt).Take(8).ToListAsync(),
            TopArticles = await _db.Articles
                                .Where(a => a.Status == "published")
                                .OrderByDescending(a => a.Views).Take(5).ToListAsync(),

            // Chat stats
            TotalChatGroups = await _db.ChatGroups.CountAsync(),
            TotalChatMessages = await _db.ChatMessages.CountAsync(),
            TotalChatUsers = await _db.ChatMembers.Select(m => new { m.MemberType, m.MemberId }).Distinct().CountAsync(),
            RecentChatGroups = await _db.ChatGroups
                                .Include(g => g.Members)
                                .OrderByDescending(g => g.CreatedAt)
                                .Take(5).ToListAsync()
        };

        // Đếm tin nhắn cho các nhóm gần đây
        var recentGroupIds = vm.RecentChatGroups.Select(g => g.Id).ToList();
        ViewBag.ChatMessageCounts = await _db.ChatMessages
            .Where(m => recentGroupIds.Contains(m.GroupId))
            .GroupBy(m => m.GroupId)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.GroupId, g => g.Count);

        return View(vm);
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

    // ===== DUYỆT BÀI VIẾT =====
    [HttpPost]
    public async Task<IActionResult> ApproveArticle(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false, message = "Chưa đăng nhập" });
        var article = await _db.Articles.FindAsync(id);
        if (article == null) return Json(new { success = false, message = "Không tìm thấy bài viết" });

        article.Status = "published";
        if (!article.PublishedAt.HasValue)
            article.PublishedAt = DateTime.Now;
        article.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();

        return Json(new { success = true });
    }

    // ===== TỪ CHỐI BÀI VIẾT =====
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

    public class RejectRequest
    {
        public string? Reason { get; set; }
    }

    // Xóa bài viết (AJAX)
    [HttpPost]
    public async Task<IActionResult> DeleteArticle(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (!IsSuperAdmin) return Json(new { success = false, message = "Bạn không đủ quyền thực hiện thao tác này." });
        var article = await _db.Articles.FindAsync(id);
        if (article == null) return Json(new { success = false });
        _db.Articles.Remove(article);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

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

    // ===== CATEGORIES (CRUD + tìm kiếm) =====
    public async Task<IActionResult> Categories()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var cats = await _db.Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync();
        var counts = await _db.Articles
            .Where(a => a.CategoryId != null)
            .GroupBy(a => a.CategoryId!.Value)
            .Select(g => new { Id = g.Key, C = g.Count() })
            .ToListAsync();
        foreach (var c in cats)
            c.ArticleCount = counts.FirstOrDefault(x => x.Id == c.Id)?.C ?? 0;
        return View(cats);
    }

    // Sinh slug duy nhất (thêm -2, -3... nếu trùng). excludeId để bỏ qua chính nó khi sửa.
    private async Task<string> UniqueCategorySlugAsync(string baseSlug, int? excludeId)
    {
        if (string.IsNullOrWhiteSpace(baseSlug)) baseSlug = "danh-muc";
        var slug = baseSlug;
        int n = 1;
        while (await _db.Categories.AnyAsync(c => c.Slug == slug && (excludeId == null || c.Id != excludeId.Value)))
            slug = $"{baseSlug}-{++n}";
        return slug;
    }

    public IActionResult CreateCategory()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        return View(new Category());
    }

    [HttpPost]
    public async Task<IActionResult> CreateCategory(Category form)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (string.IsNullOrWhiteSpace(form.Name))
        {
            ViewBag.Error = "Vui lòng nhập tên danh mục.";
            return View(form);
        }
        var baseSlug = WisdomITNews.Services.SlugHelper.MakeSlug(
            string.IsNullOrWhiteSpace(form.Slug) ? form.Name : form.Slug);
        var cat = new Category
        {
            Name = form.Name.Trim(),
            Slug = await UniqueCategorySlugAsync(baseSlug, null),
            Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim(),
            Icon = string.IsNullOrWhiteSpace(form.Icon) ? null : form.Icon.Trim(),
            Color = string.IsNullOrWhiteSpace(form.Color) ? "#e63946" : form.Color.Trim(),
            SortOrder = form.SortOrder,
            IsVisible = form.IsVisible,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã thêm danh mục.";
        return RedirectToAction("Categories");
    }

    public async Task<IActionResult> EditCategory(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return RedirectToAction("Categories");
        return View(cat);
    }

    [HttpPost]
    public async Task<IActionResult> EditCategory(int id, Category form)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return RedirectToAction("Categories");
        if (string.IsNullOrWhiteSpace(form.Name))
        {
            ViewBag.Error = "Vui lòng nhập tên danh mục.";
            return View(cat);
        }
        var baseSlug = WisdomITNews.Services.SlugHelper.MakeSlug(
            string.IsNullOrWhiteSpace(form.Slug) ? form.Name : form.Slug);
        cat.Name = form.Name.Trim();
        cat.Slug = await UniqueCategorySlugAsync(baseSlug, cat.Id);
        cat.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
        cat.Icon = string.IsNullOrWhiteSpace(form.Icon) ? null : form.Icon.Trim();
        cat.Color = string.IsNullOrWhiteSpace(form.Color) ? "#e63946" : form.Color.Trim();
        cat.SortOrder = form.SortOrder;
        cat.IsVisible = form.IsVisible;
        cat.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã cập nhật danh mục.";
        return RedirectToAction("Categories");
    }

    [HttpPost]
    public async Task<IActionResult> ToggleCategoryVisible(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return Json(new { success = false });
        cat.IsVisible = !cat.IsVisible;
        cat.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Json(new { success = true, isVisible = cat.IsVisible });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return Json(new { success = false, message = "Không tìm thấy danh mục." });

        if (await _db.Articles.AnyAsync(a => a.CategoryId == id))
            return Json(new { success = false, message = "Danh mục còn bài viết. Hãy chuyển bài sang danh mục khác rồi mới xóa." });
        if (await _db.Categories.AnyAsync(c => c.ParentCategoryId == id))
            return Json(new { success = false, message = "Danh mục còn danh mục con." });

        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ===== AI LOGS =====
    public async Task<IActionResult> AILogs(DateTime? from, DateTime? to)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var query = _db.AILogs.Include(l => l.Article).AsQueryable();

        if (from.HasValue)
            query = query.Where(l => l.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(l => l.CreatedAt < to.Value.AddDays(1));

        var logs = await query.OrderByDescending(l => l.CreatedAt).Take(200).ToListAsync();
        ViewBag.FromDate = from?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = to?.ToString("yyyy-MM-dd");
        return View(logs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAILog(long id)
    {
        if (!IsLoggedIn) return Json(new { success = false, message = "Chưa đăng nhập" });
        if (!IsSuperAdmin) return Json(new { success = false, message = "Bạn không đủ quyền thực hiện thao tác này." });
        var log = await _db.AILogs.FindAsync(id);
        if (log == null) return Json(new { success = false, message = "Không tìm thấy log" });
        _db.AILogs.Remove(log);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAILogs(DateTime? from, DateTime? to, bool all = false)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) { TempData["Err"] = "Bạn không đủ quyền truy cập chức năng này."; return RedirectToAction("Dashboard"); }
        var query = _db.AILogs.AsQueryable();
        if (!all)
        {
            if (from.HasValue) query = query.Where(l => l.CreatedAt >= from.Value);
            if (to.HasValue)   query = query.Where(l => l.CreatedAt < to.Value.AddDays(1));
            if (!from.HasValue && !to.HasValue)
            {
                TempData["AILogError"] = "Vui lòng chọn khoảng thời gian, hoặc bấm \"Xóa tất cả\".";
                return RedirectToAction("AILogs");
            }
        }
        var count = await query.CountAsync();
        if (count == 0)
        {
            TempData["AILogError"] = "Không có log nào để xóa.";
            return RedirectToAction("AILogs", new { from, to });
        }
        _db.AILogs.RemoveRange(query);
        await _db.SaveChangesAsync();
        TempData["AILogSuccess"] = $"Đã xóa {count} log AI.";
        return RedirectToAction("AILogs");
    }

    // ===== FEEDBACK =====
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

    // ===== NEWSLETTER =====
    [HttpGet]
    public async Task<IActionResult> Newsletter(string filter = "all")
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) { TempData["Err"] = "Bạn không đủ quyền truy cập chức năng này."; return RedirectToAction("Dashboard"); }
        var q = _db.NewsletterSubscribers.AsQueryable();
        if (filter == "active") q = q.Where(n => n.Status == "active");
        else if (filter == "inactive") q = q.Where(n => n.Status != "active");
        var subs = await q.OrderByDescending(n => n.SubscribedAt).Take(500).ToListAsync();
        ViewBag.SmtpConfigured = _email.IsConfigured;
        ViewBag.Filter = filter;
        return View(subs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendNewsletter(string subject, string htmlBody)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) { TempData["Err"] = "Bạn không đủ quyền truy cập chức năng này."; return RedirectToAction("Dashboard"); }
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(htmlBody))
        {
            TempData["NewsletterError"] = "Vui lòng nhập tiêu đề và nội dung email.";
            return RedirectToAction("Newsletter");
        }

        if (!_email.IsConfigured)
        {
            TempData["NewsletterError"] = "SMTP chưa được cấu hình. Hãy điền section \"Smtp\" trong appsettings.json trước.";
            return RedirectToAction("Newsletter");
        }

        var emails = await _db.NewsletterSubscribers
            .Where(n => n.Status == "active")
            .Select(n => n.Email)
            .ToListAsync();

        if (emails.Count == 0)
        {
            TempData["NewsletterError"] = "Không có subscriber nào để gửi.";
            return RedirectToAction("Newsletter");
        }

        var (ok, fail) = await _email.SendBulkAsync(emails, subject.Trim(), htmlBody);
        TempData["NewsletterSuccess"] =
            $"Đã gửi xong: {ok} thành công, {fail} thất bại (tổng {emails.Count} subscribers).";
        return RedirectToAction("Newsletter");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTestEmail(string testEmail)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) { TempData["Err"] = "Bạn không đủ quyền truy cập chức năng này."; return RedirectToAction("Dashboard"); }
        if (string.IsNullOrWhiteSpace(testEmail) || !testEmail.Contains('@'))
        {
            TempData["NewsletterError"] = "Email test không hợp lệ.";
            return RedirectToAction("Newsletter");
        }

        var (ok, err) = await _email.SendAsync(
            testEmail.Trim(),
            "[Wisdom IT News] Email test",
            "<p>Đây là email test từ Wisdom IT News.</p><p>Nếu bạn nhận được email này, cấu hình SMTP đã hoạt động.</p>");

        if (ok) TempData["NewsletterSuccess"] = $"Đã gửi email test tới {testEmail}.";
        else TempData["NewsletterError"] = $"Gửi thất bại: {err}";
        return RedirectToAction("Newsletter");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSubscriber(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (!IsSuperAdmin) return Json(new { success = false, message = "Bạn không đủ quyền thực hiện thao tác này." });
        var s = await _db.NewsletterSubscribers.FindAsync(id);
        if (s == null) return Json(new { success = false });
        _db.NewsletterSubscribers.Remove(s);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Sửa thông tin 1 subscriber (Họ tên, SĐT, Email, Nguồn, Trạng thái)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSubscriber(int id, string? fullName, string? phone, string email, string status, string? source)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) { TempData["Err"] = "Bạn không đủ quyền."; return RedirectToAction("Dashboard"); }
        var s = await _db.NewsletterSubscribers.FindAsync(id);
        if (s != null)
        {
            if (!string.IsNullOrWhiteSpace(email)) s.Email = email.Trim();
            s.FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim();
            s.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
            s.Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
            s.Status = status == "active" ? "active" : "inactive";
            await _db.SaveChangesAsync();
            TempData["NewsletterSuccess"] = "Đã cập nhật subscriber.";
        }
        return RedirectToAction("Newsletter");
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

    // ===== AI Moderation =====
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

            if (result.Score > 70)
                article.Status = "Rejected";
            else if (result.Score >= 40)
                article.Status = "PendingReview";
            else if (article.Status != "draft" && article.Status != "published")
                article.Status = "PendingApproval";

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

    // ===== USER MANAGEMENT =====
    public async Task<IActionResult> Users()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) { TempData["Err"] = "Bạn không đủ quyền truy cập chức năng này."; return RedirectToAction("Dashboard"); }
        var users = await _db.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
        return View(users);
    }

    [HttpGet]
    public async Task<IActionResult> UserDetail(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) { TempData["Err"] = "Bạn không đủ quyền truy cập chức năng này."; return RedirectToAction("Dashboard"); }
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        var commentCount = await _db.Comments.CountAsync(c => c.UserId == id);
        var articles = await _db.Articles
            .Include(a => a.Category)
            .Where(a => a.AuthorUserId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(20).ToListAsync();

        ViewBag.CommentCount = commentCount;
        ViewBag.UserArticles = articles;
        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteUser(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (!IsSuperAdmin) return Json(new { success = false, message = "Bạn không đủ quyền thực hiện thao tác này." });
        var user = await _db.Users.FindAsync(id);
        if (user == null) return Json(new { success = false });
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }
    [HttpPost]
    public async Task<IActionResult> LockUser(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (!IsSuperAdmin) return Json(new { success = false, message = "Bạn không đủ quyền thực hiện thao tác này." });
        var user = await _db.Users.FindAsync(id);
        if (user == null) return Json(new { success = false, message = "Không tìm thấy user" });

        user.IsActive = false;
        await _db.SaveChangesAsync();
        return Json(new { success = true, message = "Đã khoá tài khoản" });
    }

    [HttpPost]
    public async Task<IActionResult> UnlockUser(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (!IsSuperAdmin) return Json(new { success = false, message = "Bạn không đủ quyền thực hiện thao tác này." });
        var user = await _db.Users.FindAsync(id);
        if (user == null) return Json(new { success = false, message = "Không tìm thấy user" });

        user.IsActive = true;
        await _db.SaveChangesAsync();
        return Json(new { success = true, message = "Đã mở khoá tài khoản" });
    }

    [HttpPost]
    public async Task<IActionResult> ChangeUserRole(int id, string role)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (!IsSuperAdmin) return Json(new { success = false, message = "Bạn không đủ quyền thực hiện thao tác này." });
        var validRoles = new[] { "Reader", "Journalist", "Admin" };
        if (!validRoles.Contains(role))
            return Json(new { success = false, message = "Role không hợp lệ" });

        var user = await _db.Users.FindAsync(id);
        if (user == null) return Json(new { success = false, message = "Không tìm thấy user" });

        user.Role = role;
        await _db.SaveChangesAsync();
        return Json(new { success = true, message = $"Đã đổi role thành {role}" });

    }

    [HttpGet]
    public IActionResult CreateUser()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) { TempData["Err"] = "Bạn không đủ quyền truy cập chức năng này."; return RedirectToAction("Dashboard"); }
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(string username, string email, string password, string fullName, string role)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) { TempData["Err"] = "Bạn không đủ quyền truy cập chức năng này."; return RedirectToAction("Dashboard"); }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fullName))
        {
            ViewBag.Error = "Vui lòng điền đầy đủ thông tin";
            return View();
        }

        var lowerUsername = username.Trim().ToLowerInvariant();
        var lowerEmail = email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Username == lowerUsername))
        {
            ViewBag.Error = "Tên đăng nhập đã tồn tại";
            return View();
        }
        if (await _db.Users.AnyAsync(u => u.Email == lowerEmail))
        {
            ViewBag.Error = "Email đã được đăng ký";
            return View();
        }

        var validRoles = new[] { "Reader", "Journalist", "Admin" };
        if (!validRoles.Contains(role)) role = "Reader";

        var user = new User
        {
            Username = lowerUsername,
            Email = lowerEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FullName = fullName.Trim(),
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.Now
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return RedirectToAction("UserDetail", new { id = user.Id });
    }


    // Admin xóa tin nhắn bất kỳ
    [HttpPost]
    public async Task<IActionResult> AdminDeleteMessage(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (!IsSuperAdmin) return Json(new { success = false, message = "Bạn không đủ quyền thực hiện thao tác này." });

        var msg = await _db.ChatMessages.FindAsync(id);
        if (msg == null) return Json(new { success = false, message = "Không tìm thấy tin nhắn" });

        var groupId = msg.GroupId;
        _db.ChatMessages.Remove(msg);
        await _db.SaveChangesAsync();

        return Json(new { success = true, groupId });
    }

    // Admin kick thành viên khỏi nhóm
    [HttpPost]
    public async Task<IActionResult> KickMember(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (!IsSuperAdmin) return Json(new { success = false, message = "Bạn không đủ quyền thực hiện thao tác này." });

        var member = await _db.ChatMembers.FindAsync(id);
        if (member == null) return Json(new { success = false, message = "Không tìm thấy thành viên" });

        _db.ChatMembers.Remove(member);
        await _db.SaveChangesAsync();

        return Json(new { success = true });
    }


    // ===== HELPER =====
    // ===================== QUẢN LÝ NHÂN VIÊN (chỉ superadmin) =====================
    public async Task<IActionResult> Staff()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) { TempData["Err"] = "Chỉ Super Admin mới quản lý nhân viên."; return RedirectToAction("Dashboard"); }
        var staff = await _db.Admins.OrderByDescending(a => a.CreatedAt).ToListAsync();
        return View(staff);
    }

    [HttpGet]
    public IActionResult CreateStaff()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) return RedirectToAction("Dashboard");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStaff(string username, string email, string password, string fullName, string role, string? gender, string? address)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) return RedirectToAction("Dashboard");

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fullName))
        {
            ViewBag.Error = "Vui lòng điền đầy đủ thông tin.";
            return View();
        }
        var uname = username.Trim().ToLowerInvariant();
        if (await _db.Admins.AnyAsync(a => a.Username == uname))
        {
            ViewBag.Error = "Tên đăng nhập đã tồn tại.";
            return View();
        }
        var validRoles = new[] { "superadmin", "editor" };
        if (!validRoles.Contains(role)) role = "editor";

        _db.Admins.Add(new Admin
        {
            Username = uname,
            Email = email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FullName = fullName.Trim(),
            Role = role,
            Gender = string.IsNullOrWhiteSpace(gender) ? null : gender.Trim(),
            Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            IsActive = true,
            EmploymentStatus = "working",
            CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã tạo nhân viên mới.";
        return RedirectToAction("Staff");
    }

    [HttpGet]
    public async Task<IActionResult> EditStaff(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) return RedirectToAction("Dashboard");
        var staff = await _db.Admins.FindAsync(id);
        if (staff == null) return RedirectToAction("Staff");
        return View(staff);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditStaff(int id, string fullName, string email, string role, string? gender, string? address)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) return RedirectToAction("Dashboard");
        var staff = await _db.Admins.FindAsync(id);
        if (staff == null) return RedirectToAction("Staff");

        if (staff.Role == "superadmin" && role != "superadmin")
        {
            var superCount = await _db.Admins.CountAsync(a => a.Role == "superadmin" && a.IsActive);
            if (superCount <= 1) { TempData["Err"] = "Không thể hạ quyền Super Admin cuối cùng."; return RedirectToAction("EditStaff", new { id }); }
        }
        var validRoles = new[] { "superadmin", "editor" };
        if (!validRoles.Contains(role)) role = "editor";

        staff.FullName = (fullName ?? "").Trim();
        staff.Email = (email ?? "").Trim();
        staff.Role = role;
        staff.Gender = string.IsNullOrWhiteSpace(gender) ? null : gender.Trim();
        staff.Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã cập nhật nhân viên.";
        return RedirectToAction("Staff");
    }

    // Đổi trạng thái nhân viên (4 mã) — thay cho khoá/mở cũ.
    // IsActive tự đồng bộ: chỉ "working" (Đang làm việc) mới đăng nhập được.
    [HttpPost]
    public async Task<IActionResult> SetStaffStatus(int id, string status)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (!IsSuperAdmin) return Json(new { success = false, message = "Không đủ quyền." });

        var valid = new[] { "working", "on_leave", "resigned", "terminated" };
        if (string.IsNullOrWhiteSpace(status) || !valid.Contains(status))
            return Json(new { success = false, message = "Trạng thái không hợp lệ." });

        var staff = await _db.Admins.FindAsync(id);
        if (staff == null) return Json(new { success = false });
        if (staff.Id == AdminId) return Json(new { success = false, message = "Không thể tự đổi trạng thái của chính mình." });

        // Không cho Super Admin đang làm việc cuối cùng chuyển sang trạng thái nghỉ
        if (staff.Role == "superadmin" && status != "working")
        {
            var superWorking = await _db.Admins.CountAsync(a => a.Role == "superadmin" && a.IsActive);
            if (superWorking <= 1) return Json(new { success = false, message = "Không thể cho Super Admin cuối cùng nghỉ." });
        }

        staff.EmploymentStatus = status;
        staff.IsActive = (status == "working"); // đồng bộ cổng đăng nhập
        await _db.SaveChangesAsync();
        return Json(new { success = true, status = staff.EmploymentStatus, active = staff.IsActive });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetStaffPassword(int id, string newPassword)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) return RedirectToAction("Dashboard");
        var staff = await _db.Admins.FindAsync(id);
        if (staff != null && !string.IsNullOrWhiteSpace(newPassword))
        {
            staff.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword.Trim());
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Đã đặt lại mật khẩu.";
        }
        return RedirectToAction("EditStaff", new { id });
    }

    // ===================== ĐỐI TÁC: QUẢN LÝ NHÀ BÁO (admin + quản lý) =====================
    public async Task<IActionResult> Partners(string filter = "all", string q = "")
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var query = _db.Users.Where(u => u.Role == "Journalist");
        if (filter == "deleted") query = query.Where(u => u.IsDeleted);
        else
        {
            query = query.Where(u => !u.IsDeleted);
            if (filter == "active") query = query.Where(u => u.IsActive);
            else if (filter == "inactive") query = query.Where(u => !u.IsActive);
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var k = q.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(k) || u.Email.ToLower().Contains(k) || u.Username.ToLower().Contains(k));
        }
        var journalists = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
        var ids = journalists.Select(u => u.Id).ToList();
        ViewBag.ArticleCounts = await _db.Articles
            .Where(a => a.AuthorUserId != null && ids.Contains(a.AuthorUserId.Value))
            .GroupBy(a => a.AuthorUserId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count);
        ViewBag.Profiles = await _db.JournalistProfiles.Where(pr => ids.Contains(pr.UserId)).ToDictionaryAsync(pr => pr.UserId, pr => pr);
        ViewBag.Filter = filter; ViewBag.Q = q;
        return View(journalists);
    }

    // ====== THÊM / SỬA / XEM / XÓA MỀM / KHÔI PHỤC NHÀ BÁO ======
    [HttpGet]
    public async Task<IActionResult> CreateJournalist()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        ViewBag.Categories = await _db.Categories.OrderBy(c => c.SortOrder).ToListAsync();
        ViewBag.Profile = new JournalistProfile();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateJournalist(string fullName, string username, string email, string password, string? bio, IFormFile? avatarFile, JournalistProfile profile)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        ViewBag.Categories = await _db.Categories.OrderBy(c => c.SortOrder).ToListAsync();
        ViewBag.Profile = profile;
        ViewBag.FullName = fullName; ViewBag.Username = username; ViewBag.Email = email;
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        { ViewBag.Error = "Vui lòng điền họ tên, tên đăng nhập, email và mật khẩu."; return View(); }
        var uname = username.Trim().ToLowerInvariant(); var mail = email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Username == uname)) { ViewBag.Error = "Tên đăng nhập đã tồn tại."; return View(); }
        if (await _db.Users.AnyAsync(u => u.Email == mail)) { ViewBag.Error = "Email đã được dùng."; return View(); }

        var user = new User
        {
            Username = uname, Email = mail, FullName = fullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "Journalist", IsActive = true, IsEmailConfirmed = true, IsDeleted = false, CreatedAt = DateTime.Now,
            Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim()
        };
        if (avatarFile != null && avatarFile.Length > 0)
        {
            var up = await _imageUpload.SaveAsync(avatarFile);
            if (up.Success) user.AvatarUrl = up.RelativePath;
        }
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        profile.UserId = user.Id; profile.CreatedAt = DateTime.Now; profile.UpdatedAt = DateTime.Now;
        _db.JournalistProfiles.Add(profile);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã thêm nhà báo mới.";
        return RedirectToAction("Partners");
    }

    [HttpGet]
    public async Task<IActionResult> EditJournalist(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == "Journalist");
        if (user == null) return RedirectToAction("Partners");
        ViewBag.Profile = await _db.JournalistProfiles.FirstOrDefaultAsync(p => p.UserId == id) ?? new JournalistProfile { UserId = id };
        ViewBag.Categories = await _db.Categories.OrderBy(c => c.SortOrder).ToListAsync();
        ViewBag.ArticleCount = await _db.Articles.CountAsync(a => a.AuthorUserId == id);
        ViewBag.TotalViews = await _db.Articles.Where(a => a.AuthorUserId == id).SumAsync(a => (int?)a.Views) ?? 0;
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditJournalist(int id, string fullName, string email, string? bio, IFormFile? avatarFile, JournalistProfile profile)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == "Journalist");
        if (user == null) return RedirectToAction("Partners");
        if (!string.IsNullOrWhiteSpace(fullName)) user.FullName = fullName.Trim();
        if (!string.IsNullOrWhiteSpace(email)) user.Email = email.Trim().ToLowerInvariant();
        user.Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
        if (avatarFile != null && avatarFile.Length > 0)
        {
            var up = await _imageUpload.SaveAsync(avatarFile);
            if (up.Success) user.AvatarUrl = up.RelativePath;
        }
        var ex = await _db.JournalistProfiles.FirstOrDefaultAsync(pr => pr.UserId == id);
        if (ex == null) { profile.UserId = id; profile.CreatedAt = DateTime.Now; profile.UpdatedAt = DateTime.Now; _db.JournalistProfiles.Add(profile); }
        else
        {
            ex.PenName = profile.PenName; ex.Gender = profile.Gender; ex.DateOfBirth = profile.DateOfBirth; ex.Nationality = profile.Nationality;
            ex.Address = profile.Address; ex.City = profile.City; ex.Country = profile.Country; ex.Phone = profile.Phone;
            ex.Facebook = profile.Facebook; ex.LinkedIn = profile.LinkedIn; ex.Twitter = profile.Twitter; ex.Website = profile.Website;
            ex.Zalo = profile.Zalo; ex.Telegram = profile.Telegram; ex.JobTitle = profile.JobTitle; ex.Organization = profile.Organization;
            ex.AssignedCategory = profile.AssignedCategory; ex.YearsExperience = profile.YearsExperience; ex.PressCardNo = profile.PressCardNo;
            ex.PressCardIssued = profile.PressCardIssued; ex.PressCardExpiry = profile.PressCardExpiry; ex.Expertise = profile.Expertise;
            ex.InternalNote = profile.InternalNote; ex.UpdatedAt = DateTime.Now;
        }
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã cập nhật hồ sơ nhà báo.";
        return RedirectToAction("Partners");
    }

    [HttpGet]
    public async Task<IActionResult> JournalistDetail(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == "Journalist");
        if (user == null) return RedirectToAction("Partners");
        ViewBag.Profile = await _db.JournalistProfiles.FirstOrDefaultAsync(p => p.UserId == id);
        ViewBag.ArticleCount = await _db.Articles.CountAsync(a => a.AuthorUserId == id);
        ViewBag.TotalViews = await _db.Articles.Where(a => a.AuthorUserId == id).SumAsync(a => (int?)a.Views) ?? 0;
        ViewBag.RecentArticles = await _db.Articles.Include(a => a.Category).Where(a => a.AuthorUserId == id).OrderByDescending(a => a.CreatedAt).Take(10).ToListAsync();
        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> SoftDeleteJournalist(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var u = await _db.Users.FindAsync(id);
        if (u == null || u.Role != "Journalist") return Json(new { success = false });
        u.IsDeleted = true; await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> RestoreJournalist(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var u = await _db.Users.FindAsync(id);
        if (u == null || u.Role != "Journalist") return Json(new { success = false });
        u.IsDeleted = false; await _db.SaveChangesAsync();
        return Json(new { success = true });
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

    public async Task<IActionResult> EditVideo(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var video = await _db.Videos.FindAsync(id);
        if (video == null) return RedirectToAction("Videos");
        
        ViewBag.Video = video;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(VideoUploadService.MaxSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = VideoUploadService.MaxSize)]
    public async Task<IActionResult> EditVideo(
        int id,
        string videoType,
        string? youtubeUrl,
        string title,
        string? source,
        string? description,
        string status = "published",
        IFormFile? videoFile = null)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");

        var video = await _db.Videos.FindAsync(id);
        if (video == null) return RedirectToAction("Videos");

        if (string.IsNullOrWhiteSpace(title))
        {
            ViewBag.Video = video;
            ViewBag.Error = "Vui lòng nhập tiêu đề.";
            return View();
        }

        var isUpload = videoType == "upload";

        if (isUpload)
        {
            if (videoFile != null && videoFile.Length > 0)
            {
                // Delete old file if exists
                if (video.VideoType == "upload" && !string.IsNullOrEmpty(video.VideoUrl))
                {
                    _videoUpload.DeletePhysicalFile(video.VideoUrl);
                }
                
                var upload = await _videoUpload.SaveAsync(videoFile);
                if (!upload.Success)
                {
                    ViewBag.Video = video;
                    ViewBag.Error = upload.Error ?? "Lỗi upload video.";
                    return View();
                }
                video.VideoUrl = upload.RelativePath;
                video.FileSize = upload.FileSize;
                video.VideoType = "upload";
                video.YouTubeId = "";
            }
        }
        else
        {
            var vid = YouTubeHelper.ExtractId(youtubeUrl);
            if (string.IsNullOrEmpty(vid))
            {
                ViewBag.Video = video;
                ViewBag.Error = "Vui lòng nhập link YouTube hợp lệ.";
                return View();
            }
            video.YouTubeId = vid;
            video.VideoType = "youtube";
            // Delete old file if switching from upload to youtube
            if (video.VideoType == "upload" && !string.IsNullOrEmpty(video.VideoUrl))
            {
                _videoUpload.DeletePhysicalFile(video.VideoUrl);
                video.VideoUrl = null;
                video.FileSize = null;
            }
        }

        video.Title = title.Trim();
        video.Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
        video.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        video.Status = status == "draft" ? "draft" : "published";

        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã cập nhật video.";
        return RedirectToAction("Videos");
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
    }    // ===== XUẤT THỐNG KÊ BÀI VIẾT RA EXCEL =====
    public async Task<IActionResult> ExportArticlesExcel()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var arts = await _db.Articles
            .Include(a => a.Category).Include(a => a.Author).Include(a => a.AuthorUser)
            .OrderByDescending(a => a.Views).ToListAsync();
        var commentCounts = await _db.Comments
            .GroupBy(c => c.ArticleId).Select(g => new { Id = g.Key, C = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.C);

        var headers = new[] { "ID", "Tiêu đề", "Chuyên mục", "Tác giả", "Trạng thái", "Lượt xem", "Bình luận", "Nổi bật", "Ngày đăng" };
        var rows = arts.Select(a => new[]
        {
            a.Id.ToString(),
            a.Title,
            a.Category?.Name ?? "",
            a.AuthorUser?.FullName ?? a.Author?.FullName ?? "",
            a.Status,
            a.Views.ToString(),
            (commentCounts.TryGetValue(a.Id, out var cc) ? cc : 0).ToString(),
            a.IsFeatured ? "Có" : "",
            a.PublishedAt?.ToString("dd/MM/yyyy") ?? ""
        }).ToList();

        var bytes = WisdomITNews.Services.SimpleXlsx.Build("ThongKe", headers, rows);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"thong-ke-bai-viet-{DateTime.Now:yyyyMMdd}.xlsx");
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

    // Bật/tắt nguồn
    [HttpPost]
    public async Task<IActionResult> ToggleRssSource(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var src = await _db.RssSources.FindAsync(id);
        if (src == null) return Json(new { success = false });
        src.IsActive = !src.IsActive;
        await _db.SaveChangesAsync();
        return Json(new { success = true, isActive = src.IsActive });
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

    // Thêm nguồn mới
    [HttpPost]
    public async Task<IActionResult> AddRssSource(string name, string feedUrl, string? websiteUrl, string? description, string? country, int? defaultCategoryId, int maxImport = 30)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (!IsSuperAdmin) return Json(new { success = false, message = "Không đủ quyền" });
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(feedUrl))
            return Json(new { success = false, message = "Tên và URL RSS là bắt buộc" });

        _db.RssSources.Add(new RssSource
        {
            Name = name.Trim(),
            FeedUrl = feedUrl.Trim(),
            WebsiteUrl = websiteUrl?.Trim(),
            Description = description?.Trim(),
            Country = country?.Trim(),
            DefaultCategoryId = defaultCategoryId,
            MaxImport = maxImport > 0 ? maxImport : 30,
            IsActive = true,
            CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Xóa nguồn
    [HttpPost]
    public async Task<IActionResult> DeleteRssSource(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (!IsSuperAdmin) return Json(new { success = false, message = "Không đủ quyền" });
        var src = await _db.RssSources.FindAsync(id);
        if (src == null) return Json(new { success = false });
        _db.RssSources.Remove(src);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ===== TẠO LƯỢT XEM MẪU (demo) — chỉ điền cho bài đang 0 view =====
    [HttpPost]
    public async Task<IActionResult> SeedViews()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var rnd = new Random();
        var arts = await _db.Articles.Where(a => a.Views == 0).ToListAsync();
        foreach (var a in arts)
        {
            int v = rnd.Next(80, 6000);
            if (a.IsFeatured) v += rnd.Next(2000, 9000);   // bài nổi bật cho nhiều hơn
            if (a.IsBreaking) v += rnd.Next(1000, 5000);
            a.Views = v;
        }
        if (arts.Count > 0) await _db.SaveChangesAsync();
        TempData["Ok"] = $"Đã tạo lượt xem mẫu cho {arts.Count} bài (chỉ bài đang 0 view).";
        return RedirectToAction("Articles");
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
        int? targetUserId, string? targetEmail, string icon = "bell", string iconColor = "#159aa3")
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var svc = HttpContext.RequestServices.GetRequiredService<NotificationService>();

        if (targetType == "all")
            await svc.SendSystemAsync(title, content, icon, iconColor);
        else if (targetType == "journalist")
            await svc.SendToJournalistsAsync(title, content);
        else if (targetType == "email" && !string.IsNullOrWhiteSpace(targetEmail))
            await svc.SendToEmailAsync(targetEmail, title, content);
        else if (targetType == "user" && targetUserId.HasValue)
        {
            var notif = new Notification
            {
                Code = $"NTB{DateTime.Now.Ticks % 10000:D4}",
                Title = title, Content = content,
                Type = "custom", Icon = icon, IconColor = iconColor,
                TargetType = "user", TargetUserId = targetUserId,
                SentBy = "admin", SentByAdminId = AdminId,
                CreatedAt = DateTime.Now
            };
            _db.Notifications.Add(notif);
            await _db.SaveChangesAsync();
        }

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

    // ===== Quản lý AI (chỉ superadmin) =====
    public async Task<IActionResult> AiSettings()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) return RedirectToAction("Dashboard");

        var s = await _db.AiSettings.FirstOrDefaultAsync();
        if (s == null)
        {
            s = new AiSetting();                 // mặc định = appsettings/AiDefaults
            _db.AiSettings.Add(s);
            await _db.SaveChangesAsync();
        }
        var config = HttpContext.RequestServices.GetService(typeof(IConfiguration)) as IConfiguration;
        ViewBag.HasApiKey = !string.IsNullOrWhiteSpace(config?["Gemini:ApiKey"]);
        return View(s);
    }

    [HttpPost]
    public async Task<IActionResult> AiSettings(AiSetting form)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) return RedirectToAction("Dashboard");

        var s = await _db.AiSettings.FirstOrDefaultAsync();
        if (s == null) { s = new AiSetting(); _db.AiSettings.Add(s); }

        s.Model             = string.IsNullOrWhiteSpace(form.Model) ? "models/gemini-2.5-flash" : form.Model.Trim();
        s.ApiVersion        = string.IsNullOrWhiteSpace(form.ApiVersion) ? "v1beta" : form.ApiVersion.Trim();
        var tempStr = Request.Form["Temperature"].ToString();
        var temp = double.TryParse(tempStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var tp) ? tp : form.Temperature;
        s.Temperature       = temp < 0 ? 0 : (temp > 2 ? 2 : temp);
        s.MaxOutputTokens   = form.MaxOutputTokens > 0 ? form.MaxOutputTokens : 2048;
        s.ThinkingBudget    = form.ThinkingBudget < 0 ? 0 : form.ThinkingBudget;
        s.SystemInstruction = form.SystemInstruction ?? "";
        s.SummarizeLength   = string.IsNullOrWhiteSpace(form.SummarizeLength) ? "150-200 từ" : form.SummarizeLength.Trim();
        // Template trống -> giữ mặc định để AI không hỏng
        s.SummarizeTemplate    = string.IsNullOrWhiteSpace(form.SummarizeTemplate)    ? AiDefaults.Summarize    : form.SummarizeTemplate;
        s.SuggestTitleTemplate = string.IsNullOrWhiteSpace(form.SuggestTitleTemplate) ? AiDefaults.SuggestTitle : form.SuggestTitleTemplate;
        s.ModerateTemplate     = string.IsNullOrWhiteSpace(form.ModerateTemplate)     ? AiDefaults.Moderate     : form.ModerateTemplate;
        s.ChatMaxSentences     = form.ChatMaxSentences > 0 ? form.ChatMaxSentences : 4;
        s.ChatTemplate         = string.IsNullOrWhiteSpace(form.ChatTemplate)         ? AiDefaults.Chat         : form.ChatTemplate;
        s.UpdatedAt = DateTime.Now;

        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã lưu cấu hình AI.";
        return RedirectToAction("AiSettings");
    }

    // Thử chat ngay trong trang (dùng cấu hình đã lưu)
    [HttpPost]
    public async Task<IActionResult> TestAiChat(string message)
    {
        if (!IsLoggedIn) return Json(new { success = false, reply = "Chưa đăng nhập" });
        if (!IsSuperAdmin) return Json(new { success = false, reply = "Không đủ quyền" });
        if (string.IsNullOrWhiteSpace(message)) return Json(new { success = false, reply = "Nhập câu hỏi để thử." });

        var (reply, ok) = await _ai.ChatAsync(message.Trim());
        return Json(new { success = ok, reply });
    }
}