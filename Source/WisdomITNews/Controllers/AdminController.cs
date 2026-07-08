using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
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

    // Ghi 1 dòng nhật ký hoạt động (GĐ3) — BEST-EFFORT: lỗi ghi log KHÔNG làm hỏng thao tác chính.
    // Gọi SAU khi đã SaveChanges thao tác chính.
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
            _logger.LogWarning(ex, "TryLogStaffAsync failed (action={Action}) — có thể bảng StaffActivityLogs chưa được tạo (cần Rebuild + F5).", action);
        }
    }

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

        // Bắt buộc có danh mục hợp lệ (chặn lưu với danh mục trống/không tồn tại)
        if (vm.Article.CategoryId == null || !await _db.Categories.AnyAsync(c => c.Id == vm.Article.CategoryId))
        {
            vm.Error = "Chưa phân loại danh mục hoặc danh mục không tồn tại. Vui lòng chọn danh mục hợp lệ trước khi lưu.";
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
            IsPremiumOnly = vm.Article.IsPremiumOnly,
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
        await TryLogStaffAsync("add_article", $"Đăng bài: \"{article.Title}\"");
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

        // Bắt buộc danh mục hợp lệ (chặn lưu với danh mục trống/không tồn tại)
        if (vm.Article.CategoryId == null || !await _db.Categories.AnyAsync(c => c.Id == vm.Article.CategoryId))
        {
            vm.Error = "Chưa phân loại danh mục hoặc danh mục không tồn tại. Vui lòng chọn danh mục hợp lệ trước khi lưu.";
            vm.Categories = await _db.Categories.ToListAsync();
            return View(vm);
        }

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
        article.IsPremiumOnly = vm.Article.IsPremiumOnly;
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
        try
        {
            // Gỡ liên kết các bản ghi không cascade để không bị chặn khóa ngoại
            var logs = await _db.AILogs.Where(l => l.ArticleId == id).ToListAsync();
            foreach (var l in logs) l.ArticleId = null;              // AILog.ArticleId nullable
            var saved = await _db.SavedArticles.Where(x => x.ArticleId == id).ToListAsync();
            _db.SavedArticles.RemoveRange(saved);

            _db.Articles.Remove(article);   // Comments / ArticleTags / ViewHistories tự cascade
            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DeleteArticle failed (id={Id})", id);
            return Json(new { success = false, message = "Không xóa được — bài còn dữ liệu liên quan." });
        }
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

    // ===== PRODUCTS (Gói Premium) — CRUD + tìm kiếm =====
    public async Task<IActionResult> Products(string? q, string? active)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var query = _db.SubscriptionPlans.Include(p => p.Features).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(p => p.Name.Contains(q));
        if (active == "1") query = query.Where(p => p.IsActive);
        else if (active == "0") query = query.Where(p => !p.IsActive);
        var plans = await query.OrderByDescending(p => p.Id).ToListAsync();

        var counts = await _db.UserSubscriptions.GroupBy(s => s.PlanId)
            .Select(g => new { PlanId = g.Key, C = g.Count() }).ToListAsync();
        ViewBag.SubCounts = counts.ToDictionary(x => x.PlanId, x => x.C);
        ViewBag.Q = q; ViewBag.Active = active;
        return View(plans);
    }

    public IActionResult CreateProduct()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        ViewBag.FeatureList = new List<string>();
        return View(new SubscriptionPlan { IsActive = true, DurationDays = 30 });
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct(string Name, string? Description, decimal Price, int DurationDays, int TrialDays, bool IsActive, List<string>? featureTexts)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var cleaned = (featureTexts ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
        ViewBag.FeatureList = cleaned;
        var model = new SubscriptionPlan { Name = Name ?? "", Description = Description, Price = Price, DurationDays = DurationDays, TrialDays = TrialDays, IsActive = IsActive };

        if (string.IsNullOrWhiteSpace(Name)) { ViewBag.Error = "Vui lòng nhập tên gói."; return View(model); }
        if (Price < 0) { ViewBag.Error = "Giá không được âm."; return View(model); }
        if (DurationDays <= 0) { ViewBag.Error = "Thời hạn (ngày) phải lớn hơn 0."; return View(model); }

        var plan = new SubscriptionPlan
        {
            Name = Name.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            Price = Price,
            DurationDays = DurationDays,
            TrialDays = TrialDays < 0 ? 0 : TrialDays,
            IsActive = IsActive,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        int order = 0;
        foreach (var ft in cleaned) plan.Features.Add(new PlanFeature { FeatureText = ft, SortOrder = order++ });
        _db.SubscriptionPlans.Add(plan);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã thêm gói Premium.";
        return RedirectToAction("Products");
    }

    public async Task<IActionResult> EditProduct(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var plan = await _db.SubscriptionPlans.Include(p => p.Features).FirstOrDefaultAsync(p => p.Id == id);
        if (plan == null) return RedirectToAction("Products");
        ViewBag.FeatureList = plan.Features.OrderBy(f => f.SortOrder).Select(f => f.FeatureText).ToList();
        ViewBag.Locked = await _db.UserSubscriptions.AnyAsync(s => s.PlanId == id &&
            (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial));
        return View(plan);
    }

    [HttpPost]
    public async Task<IActionResult> EditProduct(int id, string Name, string? Description, decimal Price, int DurationDays, int TrialDays, bool IsActive, List<string>? featureTexts)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var plan = await _db.SubscriptionPlans.Include(p => p.Features).FirstOrDefaultAsync(p => p.Id == id);
        if (plan == null) return RedirectToAction("Products");

        bool locked = await _db.UserSubscriptions.AnyAsync(s => s.PlanId == id &&
            (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial));
        var cleaned = (featureTexts ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
        ViewBag.FeatureList = cleaned;
        ViewBag.Locked = locked;

        if (string.IsNullOrWhiteSpace(Name)) { ViewBag.Error = "Vui lòng nhập tên gói."; return View(plan); }
        if (Price < 0) { ViewBag.Error = "Giá không được âm."; return View(plan); }

        plan.Name = Name.Trim();
        plan.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        plan.Price = Price;
        plan.IsActive = IsActive;
        if (!locked)   // chỉ đổi thời hạn/dùng thử khi CHƯA có sub Active/Trial
        {
            if (DurationDays <= 0) { ViewBag.Error = "Thời hạn (ngày) phải lớn hơn 0."; return View(plan); }
            plan.DurationDays = DurationDays;
            plan.TrialDays = TrialDays < 0 ? 0 : TrialDays;
        }
        plan.UpdatedAt = DateTime.Now;

        // Thay toàn bộ danh sách feature.
        var old = await _db.PlanFeatures.Where(f => f.PlanId == id).ToListAsync();
        _db.PlanFeatures.RemoveRange(old);
        int order = 0;
        foreach (var ft in cleaned) _db.PlanFeatures.Add(new PlanFeature { PlanId = id, FeatureText = ft, SortOrder = order++ });
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã cập nhật gói.";
        return RedirectToAction("Products");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var plan = await _db.SubscriptionPlans.FindAsync(id);
        if (plan == null) return Json(new { success = false, message = "Không tìm thấy gói." });

        bool used = await _db.UserSubscriptions.AnyAsync(s => s.PlanId == id)
                 || await _db.Transactions.AnyAsync(t => t.PlanId == id);
        if (used)
        {
            plan.IsActive = false; plan.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            return Json(new { success = true, soft = true, message = "Gói đã từng được sử dụng nên được ẩn (xóa mềm), giữ lại lịch sử." });
        }
        var feats = await _db.PlanFeatures.Where(f => f.PlanId == id).ToListAsync();
        _db.PlanFeatures.RemoveRange(feats);
        _db.SubscriptionPlans.Remove(plan);
        await _db.SaveChangesAsync();
        return Json(new { success = true, soft = false, message = "Đã xóa gói (chưa ai dùng)." });
    }

    // ===== ADMIN: SUBSCRIPTIONS (người đã đăng ký) =====
    public async Task<IActionResult> Subscriptions(string? status, string? q)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var query = _db.UserSubscriptions.Include(s => s.Plan).AsQueryable();
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<SubscriptionStatus>(status, out var st))
            query = query.Where(s => s.Status == st);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.Trim();
            var ids = await _db.Users.Where(u => u.FullName.Contains(ql) || u.Email.Contains(ql))
                                     .Select(u => u.Id).ToListAsync();
            query = query.Where(s => ids.Contains(s.UserId));
        }
        var subs = await query.OrderByDescending(s => s.Id).Take(500).ToListAsync();
        var userIds = subs.Select(s => s.UserId).Distinct().ToList();
        var uinfo = await _db.Users.Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email }).ToListAsync();
        ViewBag.UserNames = uinfo.ToDictionary(u => u.Id, u => u.FullName ?? "");
        ViewBag.UserEmails = uinfo.ToDictionary(u => u.Id, u => u.Email ?? "");
        ViewBag.Status = status; ViewBag.Q = q;
        return View(subs);
    }

    public async Task<IActionResult> SubscriptionDetail(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var sub = await _db.UserSubscriptions.Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == id);
        if (sub == null) return RedirectToAction("Subscriptions");
        ViewBag.User = await _db.Users.FindAsync(sub.UserId);
        ViewBag.Txs = await _db.Transactions
            .Where(t => t.UserSubscriptionId == id || (t.UserId == sub.UserId && t.PlanId == sub.PlanId))
            .OrderByDescending(t => t.Id).ToListAsync();
        return View(sub);
    }

    [HttpPost]
    public async Task<IActionResult> ExtendSubscription(int id, int days)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var sub = await _db.UserSubscriptions.FindAsync(id);
        if (sub == null) { TempData["Err"] = "Không tìm thấy gói."; return RedirectToAction("Subscriptions"); }
        if (days <= 0) { TempData["Err"] = "Số ngày phải lớn hơn 0."; return RedirectToAction("SubscriptionDetail", new { id }); }
        var baseDate = sub.EndDate > DateTime.Now ? sub.EndDate : DateTime.Now;
        sub.EndDate = baseDate.AddDays(days);
        if (sub.Status == SubscriptionStatus.Expired || sub.Status == SubscriptionStatus.Cancelled)
            sub.Status = SubscriptionStatus.Active;
        sub.ConfirmedAt ??= DateTime.Now;   // gia hạn tay -> có hiệu lực
        sub.Notes = (string.IsNullOrEmpty(sub.Notes) ? "" : sub.Notes + " | ")
                  + $"Gia hạn tay +{days} ngày ({DateTime.Now:dd/MM/yyyy HH:mm})";
        await _db.SaveChangesAsync();
        TempData["Ok"] = $"Đã gia hạn thêm {days} ngày. Hết hạn mới: {sub.EndDate:dd/MM/yyyy}.";
        return RedirectToAction("SubscriptionDetail", new { id });
    }

    [HttpPost]
    public async Task<IActionResult> CancelSubscriptionAdmin(int id, string? note)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var sub = await _db.UserSubscriptions.FindAsync(id);
        if (sub == null) { TempData["Err"] = "Không tìm thấy gói."; return RedirectToAction("Subscriptions"); }
        sub.Status = SubscriptionStatus.Cancelled;
        sub.Notes = (string.IsNullOrEmpty(sub.Notes) ? "" : sub.Notes + " | ")
                  + $"Hủy bởi admin ({DateTime.Now:dd/MM/yyyy HH:mm})"
                  + (string.IsNullOrWhiteSpace(note) ? "" : ": " + note.Trim());
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã hủy gói.";
        return RedirectToAction("SubscriptionDetail", new { id });
    }

    // ===== ADMIN: QUẢN LÝ KHÁCH HÀNG (Premium / Trial) =====
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

    // Khóa / mở khóa tài khoản khách hàng
    [HttpPost]
    public async Task<IActionResult> ToggleCustomerAccount(int userId)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var u = await _db.Users.FindAsync(userId);
        if (u == null || u.IsDeleted) { TempData["Err"] = "Không tìm thấy khách hàng."; return RedirectToAction("PremiumCustomers"); }
        bool wasActive = u.IsActive;
        u.IsActive = !u.IsActive;
        CustomerHelper.AddLog(_db, userId, "Admin", HttpContext.Session.GetString("AdminName") ?? "Admin",
            u.IsActive ? "Mở khóa tài khoản" : "Khóa tài khoản",
            wasActive ? "Hoạt động" : "Bị khóa", u.IsActive ? "Hoạt động" : "Bị khóa", null);
        await _db.SaveChangesAsync();
        TempData["Ok"] = u.IsActive ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản.";
        return RedirectToAction("CustomerProfile", new { id = userId });
    }

    // Hủy gói của khách hàng
    [HttpPost]
    public async Task<IActionResult> CancelCustomerSub(int subId, string? note)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var s = await _db.UserSubscriptions.FindAsync(subId);
        if (s == null) { TempData["Err"] = "Không tìm thấy gói."; return RedirectToAction("PremiumCustomers"); }
        var actor = HttpContext.Session.GetString("AdminName") ?? "Admin";
        var oldLbl = CustomerHelper.StatusLabel(s.Status);
        s.Status = SubscriptionStatus.Cancelled;
        s.Notes = (string.IsNullOrEmpty(s.Notes) ? "" : s.Notes + " | ")
                + $"Hủy bởi {actor} ({DateTime.Now:dd/MM/yyyy HH:mm})"
                + (string.IsNullOrWhiteSpace(note) ? "" : ": " + note.Trim());
        CustomerHelper.AddLog(_db, s.UserId, "Admin", actor, "Hủy gói", oldLbl, "Đã hủy", note);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã hủy gói của khách hàng.";
        return RedirectToAction("CustomerProfile", new { id = s.UserId });
    }

    // Cập nhật gói / ngày hết hạn (không thanh toán)
    [HttpPost]
    public async Task<IActionResult> UpdateCustomerPlan(int subId, int planId, DateTime endDate, string? note)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var s = await _db.UserSubscriptions.Include(x => x.Plan).FirstOrDefaultAsync(x => x.Id == subId);
        if (s == null) { TempData["Err"] = "Không tìm thấy gói."; return RedirectToAction("PremiumCustomers"); }
        var plan = await _db.SubscriptionPlans.FindAsync(planId);
        if (plan == null) { TempData["Err"] = "Gói không hợp lệ."; return RedirectToAction("CustomerProfile", new { id = s.UserId }); }
        var actor = HttpContext.Session.GetString("AdminName") ?? "Admin";

        if (s.PlanId != planId)
        {
            CustomerHelper.AddLog(_db, s.UserId, "Admin", actor, "Đổi gói", s.Plan?.Name ?? "", plan.Name, note);
            s.PlanId = planId;
        }
        if (s.EndDate.Date != endDate.Date)
        {
            CustomerHelper.AddLog(_db, s.UserId, "Admin", actor, "Đổi ngày hết hạn",
                s.EndDate.ToString("dd/MM/yyyy"), endDate.ToString("dd/MM/yyyy"), note);
            s.EndDate = endDate;
        }
        // Gia hạn về tương lai cho gói đã hết hạn/hủy -> kích hoạt lại
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

    // Đăng ký Premium thủ công cho 1 khách hàng
    [HttpPost]
    public async Task<IActionResult> RegisterPremium(int userId, int planId, int days)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var (ok, msg, uid) = await CustomerHelper.RegisterPremiumAsync(_db, userId, planId, days, "Admin", HttpContext.Session.GetString("AdminName") ?? "Admin");
        TempData[ok ? "Ok" : "Err"] = msg;
        return uid > 0 ? RedirectToAction("CustomerProfile", new { id = uid }) : RedirectToAction("PremiumCustomers");
    }

    // Gia hạn Premium (cộng thêm ngày)
    [HttpPost]
    public async Task<IActionResult> ExtendPremium(int subId, int days)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var (ok, msg, uid) = await CustomerHelper.ExtendPremiumAsync(_db, subId, days, "Admin", HttpContext.Session.GetString("AdminName") ?? "Admin");
        TempData[ok ? "Ok" : "Err"] = msg;
        return uid > 0 ? RedirectToAction("CustomerProfile", new { id = uid }) : RedirectToAction("PremiumCustomers");
    }

    // Chuyển Trial -> Premium
    [HttpPost]
    public async Task<IActionResult> ConvertTrialToPremium(int subId, int planId, int days)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var (ok, msg, uid) = await CustomerHelper.ConvertTrialAsync(_db, subId, planId, days, "Admin", HttpContext.Session.GetString("AdminName") ?? "Admin");
        TempData[ok ? "Ok" : "Err"] = msg;
        return uid > 0 ? RedirectToAction("CustomerProfile", new { id = uid }) : RedirectToAction("PremiumCustomers");
    }

    // ===== ADMIN: TRANSACTIONS =====
    private IQueryable<Transaction> FilterTransactions(string? status, string? method, DateTime? from, DateTime? to)
    {
        var query = _db.Transactions.Include(t => t.Plan).AsQueryable();
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<TransactionStatus>(status, out var ts))
            query = query.Where(t => t.Status == ts);
        if (!string.IsNullOrWhiteSpace(method))
            query = query.Where(t => t.PaymentMethodLabel.Contains(method));
        if (from != null) query = query.Where(t => t.CreatedAt >= from.Value.Date);
        if (to != null) query = query.Where(t => t.CreatedAt < to.Value.Date.AddDays(1));
        return query;
    }

    public async Task<IActionResult> Transactions(string? status, string? method, DateTime? from, DateTime? to)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var list = await FilterTransactions(status, method, from, to)
            .OrderByDescending(t => t.Id).Take(1000).ToListAsync();
        var userIds = list.Select(t => t.UserId).Distinct().ToList();
        var uinfo = await _db.Users.Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email }).ToListAsync();
        ViewBag.UserNames = uinfo.ToDictionary(u => u.Id, u => u.FullName ?? "");
        ViewBag.UserEmails = uinfo.ToDictionary(u => u.Id, u => u.Email ?? "");
        ViewBag.Status = status; ViewBag.Method = method;
        ViewBag.From = from?.ToString("yyyy-MM-dd"); ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.TotalSuccess = list.Where(t => t.Status == TransactionStatus.Success).Sum(t => t.Amount);
        return View(list);
    }

    public async Task<IActionResult> ExportTransactionsCsv(string? status, string? method, DateTime? from, DateTime? to)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var list = await FilterTransactions(status, method, from, to)
            .OrderByDescending(t => t.Id).ToListAsync();
        var userIds = list.Select(t => t.UserId).Distinct().ToList();
        var users = await _db.Users.Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Id,Email,Goi,SoTien,PhuongThuc,TrangThai,NgayTao");
        string Esc(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
        foreach (var t in list)
        {
            var email = users.ContainsKey(t.UserId) ? users[t.UserId] : "";
            sb.Append(t.Id).Append(',')
              .Append(Esc(email)).Append(',')
              .Append(Esc(t.Plan?.Name)).Append(',')
              .Append(t.Amount.ToString("0")).Append(',')
              .Append(Esc(t.PaymentMethodLabel)).Append(',')
              .Append(t.Status).Append(',')
              .Append(t.CreatedAt.ToString("yyyy-MM-dd HH:mm")).AppendLine();
        }
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var body = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = bom.Concat(body).ToArray();
        return File(bytes, "text/csv", $"transactions_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }

    // ===== ADMIN: DASHBOARD DOANH THU =====
    public async Task<IActionResult> RevenueDashboard(DateTime? aFrom, DateTime? aTo, DateTime? bFrom, DateTime? bTo)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var now = DateTime.Now;
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-11);

        var successTx = await _db.Transactions
            .Where(t => t.Status == TransactionStatus.Success && t.CreatedAt >= start)
            .Select(t => new { t.CreatedAt, t.Amount }).ToListAsync();

        var labels = new List<string>();
        var data = new List<decimal>();
        for (int i = 0; i < 12; i++)
        {
            var m = start.AddMonths(i);
            labels.Add($"{m.Month:00}/{m.Year}");
            data.Add(successTx.Where(t => t.CreatedAt.Year == m.Year && t.CreatedAt.Month == m.Month).Sum(t => t.Amount));
        }
        ViewBag.Labels = labels;
        ViewBag.Data = data;

        ViewBag.ActiveCount = await _db.UserSubscriptions.CountAsync(s =>
            s.Status == SubscriptionStatus.Active && s.ConfirmedAt != null && s.EndDate > now);
        ViewBag.TrialCount = await _db.UserSubscriptions.CountAsync(s =>
            s.Status == SubscriptionStatus.Trial && s.ConfirmedAt != null && s.EndDate > now);
        ViewBag.TotalRevenue = await _db.Transactions
            .Where(t => t.Status == TransactionStatus.Success).SumAsync(t => (decimal?)t.Amount) ?? 0m;

        var monthStart = new DateTime(now.Year, now.Month, 1);
        int expiredThisMonth = await _db.UserSubscriptions.CountAsync(s =>
            s.Status == SubscriptionStatus.Expired && s.EndDate >= monthStart && s.EndDate <= now);
        int activeNow = await _db.UserSubscriptions.CountAsync(s =>
            s.Status == SubscriptionStatus.Active && s.EndDate > now);
        int denom = expiredThisMonth + activeNow;
        ViewBag.ExpiredRate = denom > 0 ? Math.Round(expiredThisMonth * 100.0 / denom, 1) : 0.0;
        ViewBag.ExpiredThisMonth = expiredThisMonth;

        // ===== Đối chiếu doanh thu (Ngày / Tuần / Tháng / Năm) =====
        var successAll = await _db.Transactions
            .Where(t => t.Status == TransactionStatus.Success)
            .Select(t => new { t.CreatedAt, t.Amount }).ToListAsync();
        decimal SumIn(DateTime a, DateTime b) => successAll.Where(t => t.CreatedAt >= a && t.CreatedAt < b).Sum(t => t.Amount);
        int CntIn(DateTime a, DateTime b) => successAll.Count(t => t.CreatedAt >= a && t.CreatedAt < b);

        var today = now.Date;
        var yesterday = today.AddDays(-1);
        int wdiff = ((int)today.DayOfWeek + 6) % 7;              // Thứ 2 = 0
        var weekStart = today.AddDays(-wdiff);
        var lastWeekStart = weekStart.AddDays(-7);
        var mStart = new DateTime(now.Year, now.Month, 1);
        var lastMStart = mStart.AddMonths(-1);
        var yStart = new DateTime(now.Year, 1, 1);
        var lastYStart = yStart.AddYears(-1);

        var compares = new List<RevenueCompareRow>
        {
            new() { Key="day", CurLabel="Hôm nay", PrevLabel="Hôm qua",
                Current=SumIn(today, today.AddDays(1)), Previous=SumIn(yesterday, today),
                CurCount=CntIn(today, today.AddDays(1)), PrevCount=CntIn(yesterday, today) },
            new() { Key="week", CurLabel="Tuần này", PrevLabel="Tuần trước",
                Current=SumIn(weekStart, weekStart.AddDays(7)), Previous=SumIn(lastWeekStart, weekStart),
                CurCount=CntIn(weekStart, weekStart.AddDays(7)), PrevCount=CntIn(lastWeekStart, weekStart) },
            new() { Key="month", CurLabel="Tháng này", PrevLabel="Tháng trước",
                Current=SumIn(mStart, mStart.AddMonths(1)), Previous=SumIn(lastMStart, mStart),
                CurCount=CntIn(mStart, mStart.AddMonths(1)), PrevCount=CntIn(lastMStart, mStart) },
            new() { Key="year", CurLabel="Năm " + now.Year, PrevLabel="Năm " + (now.Year-1),
                Current=SumIn(yStart, yStart.AddYears(1)), Previous=SumIn(lastYStart, yStart),
                CurCount=CntIn(yStart, yStart.AddYears(1)), PrevCount=CntIn(lastYStart, yStart) },
        };

        // Kỳ tùy chọn: so sánh 2 khoảng ngày bất kỳ.
        bool hasCustom = aFrom != null && aTo != null && bFrom != null && bTo != null;
        if (hasCustom)
        {
            compares.Add(new()
            {
                Key = "custom",
                CurLabel = $"{aFrom:dd/MM/yyyy} → {aTo:dd/MM/yyyy}",
                PrevLabel = $"{bFrom:dd/MM/yyyy} → {bTo:dd/MM/yyyy}",
                Current = SumIn(aFrom!.Value.Date, aTo!.Value.Date.AddDays(1)),
                Previous = SumIn(bFrom!.Value.Date, bTo!.Value.Date.AddDays(1)),
                CurCount = CntIn(aFrom.Value.Date, aTo.Value.Date.AddDays(1)),
                PrevCount = CntIn(bFrom.Value.Date, bTo.Value.Date.AddDays(1)),
            });
        }
        ViewBag.Compares = compares;
        ViewBag.HasCustom = hasCustom;
        ViewBag.AFrom = aFrom?.ToString("yyyy-MM-dd");
        ViewBag.ATo = aTo?.ToString("yyyy-MM-dd");
        ViewBag.BFrom = bFrom?.ToString("yyyy-MM-dd");
        ViewBag.BTo = bTo?.ToString("yyyy-MM-dd");
        return View();
    }

    // ===== XUẤT EXCEL DOANH THU (toàn bộ hoặc theo khoảng thời gian) =====
    public async Task<IActionResult> ExportRevenueExcel(DateTime? from, DateTime? to)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var q = _db.Transactions.Include(t => t.Plan).Where(t => t.Status == TransactionStatus.Success);
        if (from != null) q = q.Where(t => t.CreatedAt >= from.Value.Date);
        if (to != null) q = q.Where(t => t.CreatedAt < to.Value.Date.AddDays(1));
        var list = await q.OrderByDescending(t => t.Id).ToListAsync();
        var uids = list.Select(t => t.UserId).Distinct().ToList();
        var emails = await _db.Users.Where(u => uids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email);

        var headers = new[] { "Mã GD", "Email", "Gói", "Số tiền (đ)", "Phương thức", "Ngày" };
        var rows = list.Select(t => new[]
        {
            t.Id.ToString(),
            emails.TryGetValue(t.UserId, out var e) ? e : "",
            t.Plan?.Name ?? "",
            t.Amount.ToString("0"),
            t.PaymentMethodLabel,
            t.CreatedAt.ToString("dd/MM/yyyy HH:mm")
        }).ToList();
        rows.Add(new[] { "", "", "TỔNG CỘNG", list.Sum(t => t.Amount).ToString("0"), "", "" });

        var suffix = (from == null && to == null) ? "toan-bo" : $"{from:yyyyMMdd}-{to:yyyyMMdd}";
        var bytes = WisdomITNews.Services.SimpleXlsx.Build("DoanhThu", headers, rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"doanh-thu-{suffix}.xlsx");
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

    // ===== CHẨN ĐOÁN GỬI EMAIL =====
    [HttpGet]
    public IActionResult TestEmail()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        ViewBag.Configured = _email.IsConfigured;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> TestEmail(string toEmail)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        ViewBag.Configured = _email.IsConfigured;
        ViewBag.To = toEmail;
        if (string.IsNullOrWhiteSpace(toEmail))
        { ViewBag.Result = "err"; ViewBag.Msg = "Vui lòng nhập email nhận."; return View(); }

        var (ok, err) = await _email.SendAsync(toEmail.Trim(),
            "Test email — Wisdom IT News",
            "<p>Đây là email test. Nếu bạn nhận được email này, SMTP đã hoạt động tốt.</p>", null);
        ViewBag.Result = ok ? "ok" : "err";
        ViewBag.Msg = ok
            ? "Đã gửi THÀNH CÔNG tới " + toEmail + ". Kiểm tra hộp thư (kể cả Spam)."
            : "LỖI SMTP: " + (string.IsNullOrWhiteSpace(err) ? "không rõ" : err);
        return View();
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
    public async Task<IActionResult> Customers(string? filter = "all", string? q = null, string? category = null, string? source = null)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var query = _db.NewsletterSubscribers.AsQueryable();
        if (filter == "active") query = query.Where(n => n.Status == "active");
        else if (filter == "inactive") query = query.Where(n => n.Status != "active");
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(n => n.InterestedCategory == category);
        if (!string.IsNullOrWhiteSpace(source)) query = query.Where(n => n.Source == source);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var kw = q.Trim();
            query = query.Where(n => n.Email.Contains(kw)
                || (n.FullName != null && n.FullName.Contains(kw))
                || (n.Phone != null && n.Phone.Contains(kw)));
        }
        var subs = await query.OrderByDescending(n => n.SubscribedAt).ToListAsync();
        ViewBag.Filter = filter; ViewBag.Q = q; ViewBag.Category = category; ViewBag.Source = source;
        ViewBag.Categories = await _db.Categories.OrderBy(c => c.SortOrder).Select(c => c.Name).ToListAsync();
        ViewBag.Sources = await _db.NewsletterSubscribers.Where(n => n.Source != null).Select(n => n.Source!).Distinct().ToListAsync();
        return View(subs);
    }

    public async Task<IActionResult> CustomerDetail(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var c = await _db.NewsletterSubscribers.FindAsync(id);
        if (c == null) return RedirectToAction("Customers");
        ViewBag.Emails = await _db.NewsletterEmailLogs.Where(l => l.SubscriberId == id)
            .OrderByDescending(l => l.SentAt).Take(100).ToListAsync();
        return View(c);
    }

    public async Task<IActionResult> CreateCustomer()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        ViewBag.Categories = await _db.Categories.OrderBy(c => c.SortOrder).Select(c => c.Name).ToListAsync();
        return View(new NewsletterSubscriber());
    }

    [HttpPost]
    public async Task<IActionResult> CreateCustomer(NewsletterSubscriber form)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        async Task LoadCats() { ViewBag.Categories = await _db.Categories.OrderBy(c => c.SortOrder).Select(c => c.Name).ToListAsync(); }
        if (string.IsNullOrWhiteSpace(form.Email) || !form.Email.Contains('@'))
        { ViewBag.Error = "Email không hợp lệ."; await LoadCats(); return View(form); }
        var email = form.Email.Trim().ToLowerInvariant();
        if (await _db.NewsletterSubscribers.AnyAsync(n => n.Email == email))
        { ViewBag.Error = "Email đã tồn tại."; await LoadCats(); return View(form); }
        _db.NewsletterSubscribers.Add(new NewsletterSubscriber
        {
            Email = email,
            FullName = string.IsNullOrWhiteSpace(form.FullName) ? null : form.FullName.Trim(),
            Phone = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim(),
            Source = string.IsNullOrWhiteSpace(form.Source) ? "Thêm thủ công" : form.Source.Trim(),
            InterestedCategory = string.IsNullOrWhiteSpace(form.InterestedCategory) ? null : form.InterestedCategory,
            Status = string.IsNullOrWhiteSpace(form.Status) ? "active" : form.Status,
            SubscribedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã thêm khách hàng.";
        return RedirectToAction("Customers");
    }

    public async Task<IActionResult> EditCustomer(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var c = await _db.NewsletterSubscribers.FindAsync(id);
        if (c == null) return RedirectToAction("Customers");
        ViewBag.Categories = await _db.Categories.OrderBy(x => x.SortOrder).Select(x => x.Name).ToListAsync();
        return View(c);
    }

    [HttpPost]
    public async Task<IActionResult> EditCustomer(int id, NewsletterSubscriber form)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var c = await _db.NewsletterSubscribers.FindAsync(id);
        if (c == null) return RedirectToAction("Customers");
        if (!string.IsNullOrWhiteSpace(form.Email) && form.Email.Contains('@')) c.Email = form.Email.Trim().ToLowerInvariant();
        c.FullName = string.IsNullOrWhiteSpace(form.FullName) ? null : form.FullName.Trim();
        c.Phone = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim();
        c.Source = string.IsNullOrWhiteSpace(form.Source) ? null : form.Source.Trim();
        c.InterestedCategory = string.IsNullOrWhiteSpace(form.InterestedCategory) ? null : form.InterestedCategory;
        c.Status = string.IsNullOrWhiteSpace(form.Status) ? c.Status : form.Status;
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã cập nhật khách hàng.";
        return RedirectToAction("CustomerDetail", new { id });
    }

    // ===== Gửi email theo nhóm (segment) =====
    public async Task<IActionResult> SendCustomerEmail()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) return RedirectToAction("Dashboard");
        ViewBag.Categories = await _db.Categories.OrderBy(c => c.SortOrder).Select(c => c.Name).ToListAsync();
        ViewBag.Sources = await _db.NewsletterSubscribers.Where(n => n.Source != null).Select(n => n.Source!).Distinct().ToListAsync();
        ViewBag.SmtpConfigured = _email.IsConfigured;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> SendCustomerEmail(string segment, string? category, string? source, string subject, string htmlBody, bool insertAd = false)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) return RedirectToAction("Dashboard");
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(htmlBody))
        { TempData["Err"] = "Vui lòng nhập tiêu đề và nội dung."; return RedirectToAction("SendCustomerEmail"); }

        var query = _db.NewsletterSubscribers.Where(n => n.Status == "active");
        string segLabel = "Tất cả (active)";
        if (segment == "category" && !string.IsNullOrWhiteSpace(category))
        { query = query.Where(n => n.InterestedCategory == category); segLabel = "Danh mục: " + category; }
        else if (segment == "source" && !string.IsNullOrWhiteSpace(source))
        { query = query.Where(n => n.Source == source); segLabel = "Nguồn: " + source; }
        var recipients = await query.ToListAsync();
        if (recipients.Count == 0)
        { TempData["Err"] = "Không có khách hàng nào trong nhóm đã chọn."; return RedirectToAction("SendCustomerEmail"); }

        var body = htmlBody;
        if (insertAd)
        {
            var now = DateTime.Now;
            var ad = await _db.Advertisements
                .Where(a => a.Status == "approved" && a.IsActive
                    && (a.StartDate == null || a.StartDate <= now) && (a.EndDate == null || a.EndDate >= now))
                .OrderByDescending(a => a.Id).FirstOrDefaultAsync();
            if (ad != null && !string.IsNullOrEmpty(ad.ImageUrl))
                body += $"<hr/><div style=\"text-align:center;margin-top:16px;\"><small style=\"color:#888;\">Quảng cáo</small><br/><a href=\"{ad.TargetUrl}\"><img src=\"{ad.ImageUrl}\" style=\"max-width:100%;\"/></a></div>";
        }

        int ok = 0, fail = 0;
        foreach (var r in recipients)
        {
            bool success = false; string? err = null;
            if (_email.IsConfigured)
            {
                var res = await _email.SendAsync(r.Email, subject.Trim(), body, r.FullName);
                success = res.success; err = res.error;
            }
            else { err = "SMTP chưa cấu hình"; }
            if (success) ok++; else fail++;
            _db.NewsletterEmailLogs.Add(new NewsletterEmailLog
            {
                SubscriberId = r.Id, Email = r.Email, Subject = subject.Trim(),
                Segment = segLabel, IsSuccess = success, Error = err, SentAt = DateTime.Now
            });
        }
        await _db.SaveChangesAsync();
        TempData["Ok"] = $"Đã gửi tới nhóm '{segLabel}': {ok} thành công, {fail} thất bại (tổng {recipients.Count}).";
        return RedirectToAction("Customers");
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
    public async Task<IActionResult> SetStaffStatus(int id, string status, string? reason)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (!IsSuperAdmin) return Json(new { success = false, message = "Không đủ quyền." });

        var valid = new[] { "working", "annual_leave", "on_leave", "resigned", "terminated" };
        if (string.IsNullOrWhiteSpace(status) || !valid.Contains(status))
            return Json(new { success = false, message = "Trạng thái không hợp lệ." });
        if (string.IsNullOrWhiteSpace(reason))
            return Json(new { success = false, message = "Vui lòng nhập lý do trước khi đổi trạng thái." });

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
        staff.IsActive = (status == "working"); // chỉ "Đang làm việc" mới đăng nhập được

        // Lưu lý do đổi trạng thái vào hồ sơ nhân viên (StatusNote)
        var profile = await _db.StaffProfiles.FirstOrDefaultAsync(pr => pr.AdminId == id);
        if (profile == null) { profile = new StaffProfile { AdminId = id }; _db.StaffProfiles.Add(profile); }
        profile.StatusNote = reason.Trim();
        profile.UpdatedAt = DateTime.Now;

        var stMap = new Dictionary<string, string> { ["working"] = "Đang làm việc", ["annual_leave"] = "Nghỉ phép", ["on_leave"] = "Tạm nghỉ", ["resigned"] = "Đã nghỉ việc", ["terminated"] = "Thôi việc" };
        await _db.SaveChangesAsync();
        await TryLogStaffAsync("change_status", $"Đổi trạng thái {staff.FullName} → {(stMap.ContainsKey(status) ? stMap[status] : status)}. Lý do: {reason.Trim()}", id);
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
        ViewBag.AutoCfg = await _db.AutoImportSettings.FirstOrDefaultAsync() ?? new AutoImportSettings();
        return View(articles);
    }

    // Lưu cấu hình Tự động nhập bài (Auto Import)
    [HttpPost]
    public async Task<IActionResult> SaveAutoImportSettings(AutoImportSettings form)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");

        int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);

        var cfg = await _db.AutoImportSettings.FirstOrDefaultAsync();
        bool isNew = cfg == null;
        if (cfg == null) cfg = new AutoImportSettings();

        cfg.Enabled = form.Enabled;
        cfg.ScanIntervalSeconds = Clamp(form.ScanIntervalSeconds, 5, 86400);
        cfg.MaxPerSource = Clamp(form.MaxPerSource, 1, 500);
        cfg.DelayBetweenArticlesSeconds = Clamp(form.DelayBetweenArticlesSeconds, 0, 600);
        cfg.DelayBetweenSourcesSeconds = Clamp(form.DelayBetweenSourcesSeconds, 0, 600);
        cfg.Concurrency = Clamp(form.Concurrency, 1, 10);
        cfg.MaxTotalPerRun = Clamp(form.MaxTotalPerRun, 1, 100000);
        cfg.RetrySeconds = Clamp(form.RetrySeconds, 5, 86400);
        cfg.OnlyNew = form.OnlyNew;
        cfg.LogSuccess = form.LogSuccess;
        cfg.LogSkipDuplicate = form.LogSkipDuplicate;
        cfg.LogError = form.LogError;
        cfg.LogConnectionError = form.LogConnectionError;
        cfg.UpdatedAt = DateTime.Now;

        if (isNew) _db.AutoImportSettings.Add(cfg);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã lưu cấu hình tự động nhập bài. Tiến trình sẽ áp dụng ngay ở chu kỳ kế tiếp.";
        return RedirectToAction("RssSources");
    }

    // Lịch sử nhập (partial cho modal): danh sách bài đã nhập + lọc tiêu đề/ngày + khu "vừa nhập"
    public async Task<IActionResult> ImportHistory(string? keyword, DateTime? from, DateTime? to)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var q = _db.Articles.Include(a => a.Category).Where(a => a.IsExternal);
        if (!string.IsNullOrWhiteSpace(keyword)) q = q.Where(a => a.Title.Contains(keyword));
        if (from.HasValue) q = q.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue) q = q.Where(a => a.CreatedAt < to.Value.AddDays(1));
        var list = await q.OrderByDescending(a => a.CreatedAt).Take(200).ToListAsync();
        ViewBag.Recent = await _db.Articles.Include(a => a.Category).Where(a => a.IsExternal)
            .OrderByDescending(a => a.CreatedAt).Take(8).ToListAsync();
        return PartialView("_ImportHistory", list);
    }

    // ===== QUẢN LÝ AI: ánh xạ danh mục + sửa phân loại + nhật ký =====
    public async Task<IActionResult> AiManage()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        ViewBag.Categories   = await _db.Categories.Where(c => c.IsVisible).OrderBy(c => c.Name).ToListAsync();
        ViewBag.Mappings     = await _db.CategoryMappings.Include(m => m.Category).OrderByDescending(m => m.Id).ToListAsync();
        ViewBag.RecentArts   = await _db.Articles.Include(a => a.Category).Where(a => a.IsExternal).OrderByDescending(a => a.CreatedAt).Take(20).ToListAsync();
        ViewBag.Corrections  = await _db.AiCategoryCorrectionLogs.OrderByDescending(l => l.Id).Take(30).ToListAsync();
        return PartialView("_AiManage");
    }

    [HttpPost]
    public async Task<IActionResult> AddCategoryMapping(string aiLabel, int categoryId)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        aiLabel = (aiLabel ?? "").Trim();
        if (aiLabel.Length == 0 || await _db.Categories.FindAsync(categoryId) == null)
            return Json(new { success = false, message = "Dữ liệu không hợp lệ" });
        var existing = await _db.CategoryMappings.FirstOrDefaultAsync(m => m.AiLabel == aiLabel);
        if (existing == null) _db.CategoryMappings.Add(new CategoryMapping { AiLabel = aiLabel, CategoryId = categoryId });
        else existing.CategoryId = categoryId;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategoryMapping(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var m = await _db.CategoryMappings.FindAsync(id);
        if (m != null) { _db.CategoryMappings.Remove(m); await _db.SaveChangesAsync(); }
        return Json(new { success = true });
    }

    // Sửa danh mục AI đã gán cho 1 bài + lưu quy tắc ánh xạ + ghi nhật ký (AiCorrectionLog + AILog)
    [HttpPost]
    public async Task<IActionResult> CorrectArticleCategory(int articleId, int categoryId)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var a = await _db.Articles.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == articleId);
        var cat = await _db.Categories.FindAsync(categoryId);
        if (a == null || cat == null) return Json(new { success = false, message = "Không hợp lệ" });
        if (a.CategoryId == categoryId) return Json(new { success = true, message = "Không thay đổi" });

        var editor = HttpContext.Session.GetString("AdminName") ?? "Admin";
        var oldName = a.Category?.Name ?? "(chưa phân loại)";
        a.CategoryId = categoryId;
        a.UpdatedAt = DateTime.Now;

        // Lưu quy tắc: nhãn AI cũ -> danh mục đúng (ưu tiên áp dụng lần sau)
        if (oldName != "(chưa phân loại)")
        {
            var mp = await _db.CategoryMappings.FirstOrDefaultAsync(m => m.AiLabel == oldName);
            if (mp == null) _db.CategoryMappings.Add(new CategoryMapping { AiLabel = oldName, CategoryId = categoryId });
            else mp.CategoryId = categoryId;
        }
        _db.AiCategoryCorrectionLogs.Add(new AiCategoryCorrectionLog { ArticleId = articleId, EditorName = editor, OldCategory = oldName, NewCategory = cat.Name });
        _db.AILogs.Add(new AILog { ArticleId = articleId, Action = "classify_correct", ResultText = $"{oldName} → {cat.Name} (bởi {editor})", ModelUsed = "manual", IsSuccess = true });
        await _db.SaveChangesAsync();
        return Json(new { success = true, message = "Đã đổi danh mục + lưu quy tắc" });
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
        var (added, updated, skipped) = await svc.ImportFromSourceAsync(source, HttpContext.Session.GetString("AdminName") ?? "Admin");
        await _db.SaveChangesAsync();
        await TryLogStaffAsync("import_rss", $"Nhập {added} bài mới từ nguồn {source.Name}");

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
            var (added, updated, _) = await svc.ImportFromSourceAsync(src, HttpContext.Session.GetString("AdminName") ?? "Admin");
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

    // Bật/tắt TỰ ĐỘNG nhập (1 bài/phút) cho 1 nguồn
    [HttpPost]
    public async Task<IActionResult> ToggleAutoImport(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var src = await _db.RssSources.FindAsync(id);
        if (src == null) return Json(new { success = false });
        src.AutoImport = !src.AutoImport;
        if (src.AutoImport && !src.IsActive) src.IsActive = true;   // bật auto thì phải active
        await _db.SaveChangesAsync();
        return Json(new { success = true, autoImport = src.AutoImport, isActive = src.IsActive });
    }

    // Sửa lỗi ký tự (HTML entity) trong tiêu đề/tóm tắt các bài đã nhập
    [HttpPost]
    public async Task<IActionResult> FixArticleEntities()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var arts = await _db.Articles.Where(a => a.IsExternal).ToListAsync();
        int fixedCount = 0;
        foreach (var a in arts)
        {
            var newTitle = System.Net.WebUtility.HtmlDecode(a.Title ?? "");
            var newSummary = a.Summary == null ? null : System.Net.WebUtility.HtmlDecode(a.Summary);
            if (newTitle != a.Title || newSummary != a.Summary)
            {
                a.Title = newTitle;
                if (newSummary != null) a.Summary = newSummary;
                fixedCount++;
            }
        }
        if (fixedCount > 0) await _db.SaveChangesAsync();
        TempData["Ok"] = $"Đã sửa lỗi ký tự cho {fixedCount} bài.";
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

        await TryLogStaffAsync("post_notification", $"Đăng thông báo: {title}");
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
        ViewBag.HasGroqKey = !string.IsNullOrWhiteSpace(config?["Groq:ApiKey"]);
        ViewBag.GroqModel = config?["Groq:Model"];
        var lastLog = await _db.AILogs.OrderByDescending(l => l.Id).FirstOrDefaultAsync();
        ViewBag.LastProvider = lastLog?.ModelUsed;
        ViewBag.LastProviderAt = lastLog?.CreatedAt;
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

    // ===== HỒ SƠ NHÂN VIÊN (GĐ1) =====
    public async Task<IActionResult> StaffDetail(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var staff = await _db.Admins.FindAsync(id);
        if (staff == null) return RedirectToAction("Staff");

        var profile = await _db.StaffProfiles.FirstOrDefaultAsync(p => p.AdminId == id)
                      ?? new StaffProfile { AdminId = id };

        string? managerName = null;
        if (profile.ManagerId.HasValue)
            managerName = (await _db.Admins.FindAsync(profile.ManagerId.Value))?.FullName;

        // Lịch sử làm việc (GĐ1): bài viết nhân viên đã đăng
        var articles = await _db.Articles
            .Include(a => a.Category)
            .Where(a => a.AuthorId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(30).ToListAsync();

        ViewBag.Profile = profile;
        ViewBag.ManagerName = managerName;
        ViewBag.StaffArticles = articles;
        ViewBag.ActivityLogs = await _db.StaffActivityLogs
            .Where(l => l.TargetAdminId == id || l.ActorAdminId == id)
            .OrderByDescending(l => l.CreatedAt).Take(50).ToListAsync();
        return View(staff);
    }

    public async Task<IActionResult> EditStaffProfile(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var staff = await _db.Admins.FindAsync(id);
        if (staff == null) return RedirectToAction("Staff");
        var profile = await _db.StaffProfiles.FirstOrDefaultAsync(p => p.AdminId == id)
                      ?? new StaffProfile { AdminId = id };
        ViewBag.Profile = profile;
        ViewBag.Managers = await _db.Admins
            .Where(a => a.Role == "superadmin")
            .OrderBy(a => a.FullName).ToListAsync();
        return View(staff);
    }

    [HttpPost]
    public async Task<IActionResult> EditStaffProfile(int id, string fullName, string? email, string? gender, StaffProfile form)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var staff = await _db.Admins.FindAsync(id);
        if (staff == null) return RedirectToAction("Staff");

        static string? Nz(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        // Thông tin cốt lõi trên Admin
        if (!string.IsNullOrWhiteSpace(fullName)) staff.FullName = fullName.Trim();
        if (!string.IsNullOrWhiteSpace(email)) staff.Email = email.Trim();
        staff.Gender = Nz(gender);

        var profile = await _db.StaffProfiles.FirstOrDefaultAsync(p => p.AdminId == id);
        bool isNew = profile == null;
        if (isNew) profile = new StaffProfile { AdminId = id, CreatedAt = DateTime.Now };

        profile!.DateOfBirth = form.DateOfBirth;
        profile.PlaceOfBirth = Nz(form.PlaceOfBirth);
        profile.Nationality = Nz(form.Nationality);
        profile.IdNumber = Nz(form.IdNumber);
        profile.IdIssueDate = form.IdIssueDate;
        profile.IdIssuePlace = Nz(form.IdIssuePlace);
        profile.MaritalStatus = Nz(form.MaritalStatus);
        profile.PersonalEmail = Nz(form.PersonalEmail);
        profile.Phone = Nz(form.Phone);
        profile.PermanentAddress = Nz(form.PermanentAddress);
        profile.CurrentAddress = Nz(form.CurrentAddress);
        profile.EmergencyContactName = Nz(form.EmergencyContactName);
        profile.EmergencyContactPhone = Nz(form.EmergencyContactPhone);
        profile.Department = Nz(form.Department);
        profile.JobTitle = Nz(form.JobTitle);
        profile.Level = Nz(form.Level);
        profile.ManagerId = form.ManagerId;
        profile.JoinDate = form.JoinDate;
        profile.ContractType = Nz(form.ContractType);
        profile.ContractTerm = Nz(form.ContractTerm);
        profile.StatusNote = Nz(form.StatusNote);
        profile.ReturnDate = form.ReturnDate;
        profile.UpdatedAt = DateTime.Now;

        if (isNew) _db.StaffProfiles.Add(profile);
        await _db.SaveChangesAsync();
        await TryLogStaffAsync("edit_profile", $"Cập nhật hồ sơ nhân viên {staff.FullName}", id);
        TempData["Ok"] = "Đã lưu hồ sơ nhân viên.";
        return RedirectToAction("StaffDetail", new { id });
    }

    [HttpPost]
    public async Task<IActionResult> StaffUploadAvatar(int id, IFormFile avatarFile)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (avatarFile == null || avatarFile.Length == 0)
            return Json(new { success = false, message = "Vui lòng chọn ảnh" });
        try
        {
            var up = await _imageUpload.SaveAsync(avatarFile);
            if (!up.Success) return Json(new { success = false, message = up.Error });

            var profile = await _db.StaffProfiles.FirstOrDefaultAsync(p => p.AdminId == id);
            if (profile == null) { profile = new StaffProfile { AdminId = id }; _db.StaffProfiles.Add(profile); }
            profile.Avatar = up.RelativePath;
            profile.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            return Json(new { success = true, avatarUrl = profile.Avatar });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "StaffUploadAvatar failed");
            return Json(new { success = false, message = "Lỗi hệ thống" });
        }
    }

    // ===== NHẬT KÝ HOẠT ĐỘNG (GĐ3) — Super Admin xem + xóa =====
    public async Task<IActionResult> ActivityLog(string? q, string? actionType, int? staffId)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var query = _db.StaffActivityLogs.AsQueryable();
        if (staffId.HasValue) query = query.Where(l => l.TargetAdminId == staffId || l.ActorAdminId == staffId);
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

    [HttpPost]
    public async Task<IActionResult> DeleteActivityLog(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (!IsSuperAdmin) return Json(new { success = false, message = "Chỉ Super Admin được xóa lịch sử." });
        var log = await _db.StaffActivityLogs.FindAsync(id);
        if (log == null) return Json(new { success = false });
        _db.StaffActivityLogs.Remove(log);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteAllActivityLog()
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (!IsSuperAdmin) return Json(new { success = false, message = "Chỉ Super Admin được xóa lịch sử." });
        var all = await _db.StaffActivityLogs.ToListAsync();
        _db.StaffActivityLogs.RemoveRange(all);
        await _db.SaveChangesAsync();
        return Json(new { success = true, deleted = all.Count });
    }

    // ===== QUẢNG CÁO (GĐ1) =====
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
        {
            ViewBag.Error = "Vui lòng nhập tiêu đề và link đích.";
            return View(form);
        }
        string? img = form.ImageUrl;
        if (imageFile != null && imageFile.Length > 0)
        {
            var up = await _imageUpload.SaveAsync(imageFile);
            if (up.Success) img = up.RelativePath;
        }
        var validPos = new[] { "header", "sidebar", "in_article" };
        _db.Advertisements.Add(new Advertisement
        {
            Title = form.Title.Trim(),
            ImageUrl = img,
            TargetUrl = form.TargetUrl.Trim(),
            Position = validPos.Contains(form.Position) ? form.Position : "sidebar",
            StartDate = form.StartDate,
            EndDate = form.EndDate,
            IsActive = form.IsActive,
            Status = "approved",
            CreatedByAdminId = AdminId,
            CreatedByName = HttpContext.Session.GetString("AdminName") ?? "Admin",
            CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();
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
        {
            var up = await _imageUpload.SaveAsync(imageFile);
            if (up.Success) ad.ImageUrl = up.RelativePath;
        }
        else if (!string.IsNullOrWhiteSpace(form.ImageUrl)) ad.ImageUrl = form.ImageUrl.Trim();
        ad.Title = (form.Title ?? "").Trim();
        ad.TargetUrl = (form.TargetUrl ?? "").Trim();
        var validPos = new[] { "header", "sidebar", "in_article" };
        ad.Position = validPos.Contains(form.Position) ? form.Position : ad.Position;
        ad.StartDate = form.StartDate;
        ad.EndDate = form.EndDate;
        ad.IsActive = form.IsActive;
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
        if (!IsSuperAdmin) return Json(new { success = false, message = "Không đủ quyền." });
        var ad = await _db.Advertisements.FindAsync(id);
        if (ad == null) return Json(new { success = false });
        ad.Status = "approved"; ad.IsActive = true;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> RejectAd(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (!IsSuperAdmin) return Json(new { success = false, message = "Không đủ quyền." });
        var ad = await _db.Advertisements.FindAsync(id);
        if (ad == null) return Json(new { success = false });
        ad.Status = "rejected"; ad.IsActive = false;
        await _db.SaveChangesAsync();
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

    // ===== PODCAST / AUDIO =====
    public async Task<IActionResult> Podcasts()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var list = await _db.Podcasts.Include(p => p.Article).OrderByDescending(p => p.CreatedAt).ToListAsync();
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
        var res = await svc.SaveUploadAsync(audioFile, title, description, articleId, AdminId, "Admin");
        TempData[res.Success ? "Ok" : "Err"] = res.Success ? "Đã tải lên audio." : ("Lỗi: " + res.Error);
        return RedirectToAction("Podcasts");
    }

    [HttpPost]
    public async Task<IActionResult> DeletePodcast(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var pod = await _db.Podcasts.FindAsync(id);
        if (pod == null) return Json(new { success = false });
        try
        {
            var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var abs = Path.Combine(env.WebRootPath, pod.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(abs)) System.IO.File.Delete(abs);
        }
        catch { /* xóa file best-effort */ }
        _db.Podcasts.Remove(pod);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> GeneratePodcast(int articleId)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var svc = HttpContext.RequestServices.GetRequiredService<PodcastService>();
        var res = await svc.GenerateFromArticleAsync(articleId, AdminId, "Admin");
        return Json(new { success = res.Success, message = res.Success ? "Đã tạo audio từ bài viết." : res.Error, filePath = res.Podcast?.FilePath });
    }
}