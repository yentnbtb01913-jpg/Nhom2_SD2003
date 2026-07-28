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
    private readonly ExternalArticleService _external;
    private readonly FeaturedArticleService _featuredSvc;

    public AdminController(
        AppDbContext db,
        ImageUploadService imageUpload,
        AIService ai,
        EmailService email,
        VideoUploadService videoUpload,
        ILogger<AdminController> logger,
        IServiceProvider serviceProvider,
        ExternalArticleService external,
        FeaturedArticleService featuredSvc)
    {
        _db = db;
        _imageUpload = imageUpload;
        _ai = ai;
        _email = email;
        _videoUpload = videoUpload;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _external = external;
        _featuredSvc = featuredSvc;
    }

    // ========================================================================
    // KHU VỰC 1: HELPER & BIẾN DÙNG CHUNG (session, quyền)
    // ========================================================================
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

    // ========================================================================
    // KHU VỰC 2: BỘ LỌC PHÂN QUYỀN TOÀN CỤC
    // ========================================================================
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

    // ========================================================================
    // KHU VỰC 3: AUTH — ĐĂNG NHẬP / ĐĂNG XUẤT
    // Bảng: Admins
    // ========================================================================
    public IActionResult Login() => IsLoggedIn ? RedirectToAction("Dashboard") : View();

    [HttpPost]
    // Đây là luồng xử lý đăng nhập admin/nhân viên
    // Luồng: tìm Admin theo Username -> BCrypt.Verify -> chặn nếu !IsActive/đã nghỉ -> nạp session (AdminId/Name/Role)
    // Bảng: Admins
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

    // Đây là luồng xử lý đăng xuất admin (xóa session)
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    // ========================================================================
    // KHU VỰC 4: CỔNG QUẢN LÝ CHO NHÂN VIÊN (EDITOR)
    // Là "nhà" của nhân viên: chỉ gồm các lối tắt tới chức năng họ được phép.
    // Editor đăng nhập sẽ vào thẳng đây; superadmin vẫn dùng Dashboard tổng quan.
    // ========================================================================
    public async Task<IActionResult> Manage()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");

        ViewBag.TotalArticles = await _db.Articles.CountAsync();
        ViewBag.DraftArticles = await _db.Articles.CountAsync(a => a.Status == "draft");
        ViewBag.PendingComments = await _db.Comments.CountAsync(c => c.Status == "pending");
        ViewBag.OpenFeedbacks = await _db.FeedbackReports.CountAsync(f => !f.IsResolved);
        ViewBag.PendingPartners = await _db.Users.CountAsync(u => u.Role == "Journalist" && !u.IsActive);
        return View();
    }

    // ========================================================================
    // KHU VỰC 5: DASHBOARD TỔNG QUAN (SUPER ADMIN)
    // ========================================================================
    // Đây là luồng xử lý dashboard admin (thống kê tổng quan: bài, người dùng, doanh thu...)
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
            Subscribers = await _db.Advertisements.CountAsync(a => a.Status == "approved"), // [ĐỔI] đếm QC đang chạy (thay subscriber)
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

    // ========================================================================
    // KHU VỰC 6: QUẢN LÝ BÀI VIẾT (ARTICLES) — Danh sách / Tạo / Sửa
    // Bảng: Articles, Categories, ArticleTags, Tags, AILogs
    // ========================================================================
    // Đây là luồng xử lý danh sách bài viết (lọc theo trạng thái, phân trang)
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

    // Đây là luồng xử lý hiển thị form tạo bài viết
    public async Task<IActionResult> CreateArticle()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        return View(new ArticleFormViewModel { Categories = await _db.Categories.ToListAsync() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Đây là luồng xử lý tạo bài viết mới
    // Luồng: upload thumbnail -> sinh slug (chống trùng) -> gán tác giả (AuthorId) -> lưu Articles + tag
    //        -> kiểm duyệt ngôn từ AI (ApplyAIModerationAsync)
    // Bảng: Articles, ArticleTags, Tags, AILogs
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

    // Đây là luồng xử lý hiển thị form sửa bài viết
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
    // Đây là luồng xử lý cập nhật bài viết
    // Luồng: cập nhật nội dung + thumbnail + tag -> kiểm duyệt ngôn từ AI lại nếu nội dung đổi
    // Bảng: Articles, ArticleTags, AILogs
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

    // ========================================================================
    // KHU VỰC 7: DUYỆT / TỪ CHỐI / XÓA BÀI VIẾT
    // Bảng: Articles, Notifications, AILogs, SavedArticles
    // ========================================================================
    [HttpPost]
    // Đây là luồng xử lý duyệt bài viết (đổi Status="published", đặt PublishedAt)
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

    [HttpPost]
    // Đây là luồng xử lý từ chối bài viết (Status="rejected" + bắn thông báo cho tác giả kèm lý do)
    // Bảng: Articles, Notifications
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
            var actor = HttpContext.Session.GetString("AdminName") ?? "Quản trị viên";
            await notifSvc.SendArticleRejectedAsync(
                article.AuthorUserId.Value,
                article.Id,
                article.Title,
                req?.Reason ?? "Không đáp ứng tiêu chuẩn nội dung",
                actor
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
    // Đây là luồng xử lý xóa bài viết (kèm bình luận/lượt xem... theo cấu hình FK)
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

    // ========================================================================
    // KHU VỰC 8: QUẢN LÝ BÌNH LUẬN (COMMENTS)
    // Bảng: Comments, Articles
    // ========================================================================
    // Đây là luồng xử lý danh sách bình luận chờ duyệt (lọc theo trạng thái)
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
    // Đây là luồng xử lý duyệt bình luận (Status="approved")
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
    // Đây là luồng xử lý xóa bình luận
    public async Task<IActionResult> DeleteComment(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var c = await _db.Comments.FindAsync(id);
        if (c == null) return Json(new { success = false });
        _db.Comments.Remove(c);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ========================================================================
    // KHU VỰC 9: QUẢN LÝ DANH MỤC (CATEGORIES) — CRUD + tìm kiếm
    // Bảng: Categories, Articles
    // ========================================================================
    // Đây là luồng xử lý danh sách danh mục (chuyên mục)
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

    // Đây là luồng xử lý hiển thị form tạo danh mục
    public IActionResult CreateCategory()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        return View(new Category());
    }

    [HttpPost]
    // Đây là luồng xử lý tạo danh mục mới (sinh slug, chống trùng)
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

    // Đây là luồng xử lý hiển thị form sửa danh mục
    public async Task<IActionResult> EditCategory(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var cat = await _db.Categories.FindAsync(id);
        if (cat == null) return RedirectToAction("Categories");
        return View(cat);
    }

    [HttpPost]
    // Đây là luồng xử lý cập nhật danh mục
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
    // Đây là luồng xử lý bật/tắt hiển thị danh mục (IsVisible)
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
    // Đây là luồng xử lý xóa danh mục
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

    // [ĐÃ GỠ] Khu Premium (Products/Subscriptions/Khách hàng Premium/Transactions/Doanh thu) đã được loại bỏ — tái dùng cho Bán Quảng Cáo ở B5.
    // [ĐÃ GỠ] Action TeamChat (Chat nội bộ) đã được loại bỏ.

    // ========================================================================
    // KHU VỰC 10: CHẨN ĐOÁN GỬI EMAIL
    // ========================================================================
    [HttpGet]
    // Đây là luồng xử lý hiển thị trang chẩn đoán gửi email
    public IActionResult TestEmail()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        ViewBag.Configured = _email.IsConfigured;
        return View();
    }

    [HttpPost]
    // Đây là luồng xử lý gửi email thử để kiểm tra cấu hình SMTP
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
    // [ĐÃ GỠ] Khu Gia hạn quảng cáo (RenewalAds/AdChat/ExtendAdRenewal) đã được loại bỏ.

    // ========================================================================
    // KHU VỰC 11: NHẬT KÝ AI (AI LOGS)
    // Bảng: AILogs
    // ========================================================================
    // Đây là luồng xử lý xem nhật ký AI (gồm kết quả kiểm duyệt + ngôn từ vi phạm đã ghi)
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
    // Đây là luồng xử lý xóa 1 dòng nhật ký AI
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
    // Đây là luồng xử lý xóa nhiều nhật ký AI (theo khoảng ngày hoặc tất cả)
    public async Task<IActionResult> DeleteAILogs(DateTime? from, DateTime? to, bool all = false)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) { TempData["Err"] = "Bạn không đủ quyền truy cập chức năng này."; return RedirectToAction("Dashboard"); }
        var query = _db.AILogs.AsQueryable();
        if (!all)
        {
            if (from.HasValue) query = query.Where(l => l.CreatedAt >= from.Value);
            if (to.HasValue) query = query.Where(l => l.CreatedAt < to.Value.AddDays(1));
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

    // ========================================================================
    // KHU VỰC 12: QUẢN LÝ PHẢN HỒI (FEEDBACK)
    // Bảng: FeedbackReports
    // ========================================================================
    // Đây là luồng xử lý danh sách phản hồi/góp ý của người đọc (lọc open/resolved)
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
    // Đây là luồng xử lý đánh dấu phản hồi đã xử lý (IsResolved=true)
    public async Task<IActionResult> ResolveFeedback(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var fb = await _db.FeedbackReports.FindAsync(id);
        if (fb == null) return Json(new { success = false });
        fb.IsResolved = true;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // [ĐÃ GỠ] Khu Newsletter + Quản lý người đăng ký nhận tin đã được loại bỏ.

    // ========================================================================
    // KHU VỰC 13: KIỂM DUYỆT AI (AI MODERATION) — Hàm dùng chung
    // Bảng: AILogs, Articles, Notifications
    // ========================================================================
    // Đây là luồng xử lý kiểm duyệt ngôn từ bài viết bằng AI (dùng chung khi tạo/sửa bài)
    // Luồng: ghép tiêu đề+nội dung -> AIService.ModerateContentAsync -> ghi AILog (score + ngôn từ vi phạm "issues")
    //        -> nếu score cao thì đánh dấu bài cần xem lại. AI lỗi không chặn.
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

    // ========================================================================
    // KHU VỰC 14: QUẢN LÝ NGƯỜI DÙNG (USER MANAGEMENT) — độc giả
    // Bảng: Users, Comments, Articles
    // ========================================================================
    // Đây là luồng xử lý danh sách người dùng (độc giả)
    public async Task<IActionResult> Users()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) { TempData["Err"] = "Bạn không đủ quyền truy cập chức năng này."; return RedirectToAction("Dashboard"); }
        var users = await _db.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
        return View(users);
    }

    [HttpGet]
    // Đây là luồng xử lý xem chi tiết một người dùng
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
    // Đây là luồng xử lý xóa người dùng (soft delete IsDeleted)
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
    // Đây là luồng xử lý khóa tài khoản người dùng (IsActive=false)
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
    // Đây là luồng xử lý mở khóa tài khoản người dùng (IsActive=true)
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
    // Đây là luồng xử lý đổi vai trò người dùng
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
    // Đây là luồng xử lý hiển thị form tạo người dùng
    public IActionResult CreateUser()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) { TempData["Err"] = "Bạn không đủ quyền truy cập chức năng này."; return RedirectToAction("Dashboard"); }
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Đây là luồng xử lý tạo người dùng mới (BCrypt hash mật khẩu, chống trùng)
    // Bảng: Users
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

    // [ĐÃ GỠ] KHU VỰC 15: Admin can thiệp chat người dùng (AdminDeleteMessage / KickMember)
    // đã được loại bỏ — chat là quyền riêng tư của người dùng, admin không can thiệp.

    // ========================================================================
    // KHU VỰC 16: QUẢN LÝ NHÂN VIÊN (STAFF) — chỉ superadmin
    // Bảng: Admins, StaffProfiles, StaffActivityLogs, Articles
    // ========================================================================
    // Đây là luồng xử lý danh sách nhân viên (chỉ superadmin)
    public async Task<IActionResult> Staff()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) { TempData["Err"] = "Chỉ Super Admin mới quản lý nhân viên."; return RedirectToAction("Dashboard"); }
        var staff = await _db.Admins.OrderByDescending(a => a.CreatedAt).ToListAsync();
        return View(staff);
    }

    [HttpGet]
    // Đây là luồng xử lý hiển thị form thêm nhân viên
    public IActionResult CreateStaff()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        if (!IsSuperAdmin) return RedirectToAction("Dashboard");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Đây là luồng xử lý tạo nhân viên mới (Admin + BCrypt, chống trùng username)
    // Bảng: Admins
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
    // Đây là luồng xử lý hiển thị form sửa nhân viên
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
    // Đây là luồng xử lý cập nhật thông tin nhân viên
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
    // Đây là luồng xử lý đặt trạng thái làm việc của nhân viên (working/on_leave/resigned/terminated)
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
    // Đây là luồng xử lý đặt lại mật khẩu nhân viên (BCrypt)
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

    // ========================================================================
    // KHU VỰC 17: ĐỐI TÁC — QUẢN LÝ NHÀ BÁO (admin + quản lý)
    // Bảng: Users (Role=Journalist), JournalistProfiles, Articles
    // ========================================================================
    // Đây là luồng xử lý danh sách đối tác nhà báo (lọc + tìm kiếm)
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
    // Đây là luồng xử lý hiển thị form thêm nhà báo
    public async Task<IActionResult> CreateJournalist()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        ViewBag.Categories = await _db.Categories.OrderBy(c => c.SortOrder).ToListAsync();
        ViewBag.Profile = new JournalistProfile();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Đây là luồng xử lý tạo nhà báo mới (User Role=Journalist + JournalistProfile + upload avatar)
    // Bảng: Users, JournalistProfiles
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
            Username = uname,
            Email = mail,
            FullName = fullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "Journalist",
            IsActive = true,
            IsEmailConfirmed = true,
            IsDeleted = false,
            CreatedAt = DateTime.Now,
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
    // Đây là luồng xử lý hiển thị form sửa nhà báo
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
    // Đây là luồng xử lý cập nhật thông tin nhà báo (+ hồ sơ)
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
    // Đây là luồng xử lý xem chi tiết hồ sơ nhà báo
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
    // Đây là luồng xử lý xóa mềm nhà báo (IsDeleted=true, giữ dữ liệu)
    public async Task<IActionResult> SoftDeleteJournalist(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var u = await _db.Users.FindAsync(id);
        if (u == null || u.Role != "Journalist") return Json(new { success = false });
        u.IsDeleted = true; await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    // Đây là luồng xử lý khôi phục nhà báo đã xóa mềm
    public async Task<IActionResult> RestoreJournalist(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var u = await _db.Users.FindAsync(id);
        if (u == null || u.Role != "Journalist") return Json(new { success = false });
        u.IsDeleted = false; await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    // Đây là luồng xử lý duyệt nhà báo (kích hoạt tài khoản đối tác)
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
    // Đây là luồng xử lý khóa tài khoản nhà báo
    public async Task<IActionResult> LockJournalist(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var u = await _db.Users.FindAsync(id);
        if (u == null || u.Role != "Journalist") return Json(new { success = false });
        u.IsActive = false;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ========================================================================
    // KHU VỰC 18: ĐĂNG VIDEO (YouTube / Upload)
    // Bảng: Videos
    // ========================================================================
    // Đây là luồng xử lý danh sách video (YouTube/upload)
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

    // Đây là luồng xử lý hiển thị form thêm video
    public IActionResult CreateVideo()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        return View();
    }

    // Đây là luồng xử lý hiển thị form sửa video
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
    // Đây là luồng xử lý cập nhật video (YouTube id/upload file)
    // Bảng: Videos
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
    // Đây là luồng xử lý thêm video mới (YouTubeHelper tách id / VideoUploadService lưu file)
    // Bảng: Videos
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
    // Đây là luồng xử lý xóa video (kèm xóa file vật lý nếu là upload)
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
    }

    // ========================================================================
    // KHU VỰC 19: XUẤT THỐNG KÊ BÀI VIẾT RA EXCEL
    // ========================================================================
    // Đây là luồng xử lý xuất danh sách bài viết ra Excel
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

    // ========================================================================
    // KHU VỰC 20: QUẢN LÝ NGUỒN TIN RSS (nhập bài tự động/thủ công)
    // Bảng: RssSources, Articles, AutoImportSettings
    // ========================================================================
    // Danh sách nguồn + danh sách bài đã import (IsExternal = true)
    // Đây là luồng xử lý trang quản lý nguồn tin RSS (danh sách nguồn + lịch sử nhập, lọc)
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

        // ===== VIDEO đã import (nhập từ nguồn RSS loại Video) — có Source = tên nguồn =====
        var vq = _db.Videos.Where(v => v.Source != null && v.Source != "");
        if (sourceId.HasValue)
        {
            var vsrc = sources.FirstOrDefault(s => s.Id == sourceId);
            if (vsrc != null) vq = vq.Where(v => v.Source == vsrc.Name);
        }
        if (!string.IsNullOrWhiteSpace(keyword)) vq = vq.Where(v => v.Title.Contains(keyword));
        if (fromDate.HasValue) vq = vq.Where(v => v.PublishedAt >= fromDate.Value);
        if (toDate.HasValue) vq = vq.Where(v => v.PublishedAt < toDate.Value.AddDays(1));
        ViewBag.ImportedVideos = await vq.OrderByDescending(v => v.PublishedAt).Take(50).ToListAsync();

        return View(articles);
    }

    // Lưu cấu hình Tự động nhập bài (Auto Import)
    [HttpPost]
    // Đây là luồng xử lý lưu cấu hình tự động nhập RSS
    // Bảng: AutoImportSettings
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
    // Đây là luồng xử lý xem lịch sử nhập bài từ nguồn RSS
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

    // ========================================================================
    // [ĐÃ GỠ] KHU VỰC 21: QUẢN LÝ PHÂN LOẠI AI (ánh xạ danh mục + tự học + sửa phân loại)
    // Lý do: bỏ AI phân loại — mỗi nguồn RSS gắn 1 danh mục cố định (DefaultCategoryId).
    // Các action AiManage / AddCategoryMapping / DeleteCategoryMapping / CorrectArticleCategory đã xóa.
    // (Bảng CategoryMappings, AiCategoryCorrectionLogs vẫn giữ trong DB, không còn dùng.)
    // ========================================================================


    // Import từ 1 nguồn cụ thể
    [HttpPost]
    // Đây là luồng xử lý nhập bài từ 1 nguồn RSS (qua NewsImportService)
    public async Task<IActionResult> ImportFromSource(int id)
    {
        // Chưa đăng nhập → từ chối.
        if (!IsLoggedIn) return Json(new { success = false });

        // Tìm nguồn RSS theo id.
        var source = await _db.RssSources.FindAsync(id);
        // Không có nguồn → báo lỗi.
        if (source == null) return Json(new { success = false, message = "Không tìm thấy nguồn" });
        // Nguồn đang tắt → không cho nhập.
        if (!source.IsActive) return Json(new { success = false, message = "Nguồn này đang bị tắt" });

        // Lấy service NewsImportService từ DI container (theo scope của request hiện tại).
        var svc = HttpContext.RequestServices.GetRequiredService<NewsImportService>();
        // Gọi nhập bài từ nguồn này. Trả về: số bài THÊM mới, số bài CẬP NHẬT, số bài BỎ QUA (trùng).
        var (added, updated, skipped) = await svc.ImportFromSourceAsync(source, HttpContext.Session.GetString("AdminName") ?? "Admin");

        // Lưu thay đổi (bài mới, cập nhật...) xuống DB.
        await _db.SaveChangesAsync();
        // Ghi log hoạt động của nhân viên/admin: đã nhập bao nhiêu bài từ nguồn nào.
        await TryLogStaffAsync("import_rss", $"Nhập {added} bài mới từ nguồn {source.Name}");

        // Trả kết quả tóm tắt (thêm/cập nhật/bỏ qua).
        return Json(new { success = true, message = $"Đã nhập {added} bài mới, cập nhật {updated}, bỏ qua {skipped} từ {source.Name}" });
    }

    // Import tất cả nguồn đang active
    [HttpPost]
    // Đây là luồng xử lý nhập bài từ tất cả nguồn RSS
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
    // Đây là luồng xử lý bật/tắt một nguồn RSS (IsActive)
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
    // Đây là luồng xử lý bật/tắt tự động nhập cho một nguồn RSS (AutoImport)
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
    // Đây là luồng xử lý sửa lỗi ký tự HTML entity trong các bài đã nhập
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
    // Đây là luồng xử lý xóa 1 bài đã nhập từ nguồn ngoài
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
    // Đây là luồng xử lý xóa nhiều bài đã nhập cùng lúc
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
    // Đây là luồng xử lý thêm nguồn RSS mới
    // Bảng: RssSources
    public async Task<IActionResult> AddRssSource(string name, string feedUrl, string? websiteUrl, string? description, string? country, int? defaultCategoryId, int maxImport = 30, string? sourceType = null)
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
            SourceType = sourceType == "video" ? "video" : "article",
            IsActive = true,
            CreatedAt = DateTime.Now
        });
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Xóa nguồn
    [HttpPost]
    // Đây là luồng xử lý xóa một nguồn RSS
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

    // ========================================================================
    // KHU VỰC 22: TẠO LƯỢT XEM MẪU (demo) — chỉ điền cho bài đang 0 view
    // ========================================================================
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

    // ========================================================================
    // KHU VỰC 23: KÊNH BÊN NGOÀI (trang riêng — chỉ bài IsExternal = true)
    // ========================================================================
    public async Task<IActionResult> ExternalArticles(string? source, int page = 1)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");

        var (items, total, totalPages) = await _external.GetListAsync(source, page);
        var sources = await _external.GetSourcesAsync();

        ViewBag.Sources = sources;
        ViewBag.SelectedSource = source;
        ViewBag.Page = page;
        ViewBag.Total = total;
        ViewBag.TotalPages = totalPages;
        ViewBag.Batches = await _external.GetBatchesAsync();
        ViewBag.Categories = await _db.Categories.Where(c => c.IsVisible).OrderBy(c => c.Name).ToListAsync();
        return View(items);
    }

    [HttpPost]
    public async Task<IActionResult> SaveSourceLogo(int sourceId, string? logoUrl)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var (ok, msg) = await _external.SaveSourceLogoAsync(sourceId, logoUrl);
        return Json(new { success = ok, message = msg, logoUrl = ExternalArticleService.ResolveAvatarUrl(await _db.RssSources.FindAsync(sourceId)) });
    }

    [HttpPost]
    public async Task<IActionResult> CreateSeedViewBatch(string scope, int? articleId, int? sourceId, int? categoryId, int minViews, int maxViews, DateTime? fromDate, DateTime? toDate)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var editor = HttpContext.Session.GetString("AdminName") ?? "Admin";
        var (ok, msg, batch) = await _external.CreateSeedBatchAsync(scope, articleId, sourceId, categoryId, minViews, maxViews, fromDate, toDate, editor);
        return Json(new { success = ok, message = msg });
    }

    // Modal "Tạo lượt xem mẫu" — phạm vi "Theo bài báo cụ thể": danh sách preview để chọn + sửa trực tiếp
    [HttpGet]
    public async Task<IActionResult> SearchExternalArticlesForSeed(string? q, string? source)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var items = await _external.SearchForSeedPickerAsync(q, source);
        var list = items.Select(a => new
        {
            id = a.Id,
            title = a.Title,
            thumbnail = a.Thumbnail,
            sourceName = a.SourceName,
            categoryName = a.Category?.Name,
            views = a.Views
        });
        return Json(new { success = true, items = list });
    }

    [HttpPost]
    public async Task<IActionResult> SetArticleViews(int articleId, int views)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var (ok, msg, newViews) = await _external.SetArticleViewsAsync(articleId, views);
        return Json(new { success = ok, message = msg, views = newViews });
    }

    [HttpPost]
    public async Task<IActionResult> EditSeedViewBatch(int id, int minViews, int maxViews)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var (ok, msg) = await _external.EditSeedBatchAsync(id, minViews, maxViews);
        return Json(new { success = ok, message = msg });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteSeedViewBatch(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var (ok, msg) = await _external.DeleteSeedBatchAsync(id);
        return Json(new { success = ok, message = msg });
    }

    // ========================================================================
    // KHU VỰC 24: TIN NỔI BẬT TỰ ĐỘNG (Trang chủ) — tab quản lý chỉ xem + can thiệp Ghim/Loại
    // ========================================================================
    public async Task<IActionResult> FeaturedArticles()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        ViewBag.Ranking = await _featuredSvc.GetRankingListAsync(50);
        ViewBag.Hidden = await _featuredSvc.GetHiddenListAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> PinFeatured(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var (ok, msg) = await _featuredSvc.PinAsync(id);
        return Json(new { success = ok, message = msg });
    }

    [HttpPost]
    public async Task<IActionResult> UnpinFeatured(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var (ok, msg) = await _featuredSvc.UnpinAsync(id);
        return Json(new { success = ok, message = msg });
    }

    [HttpPost]
    public async Task<IActionResult> HideFeatured(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var (ok, msg) = await _featuredSvc.HideAsync(id);
        return Json(new { success = ok, message = msg });
    }

    [HttpPost]
    public async Task<IActionResult> UnhideFeatured(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var (ok, msg) = await _featuredSvc.UnhideAsync(id);
        return Json(new { success = ok, message = msg });
    }

    // ========================================================================
    // KHU VỰC 25: HELPER LƯU TAGS (dùng chung cho CreateArticle/EditArticle)
    // ========================================================================
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

    // ========================================================================
    // KHU VỰC 26: QUẢN LÝ THÔNG BÁO (NOTIFICATIONS)
    // Bảng: Notifications
    // ========================================================================
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
                Title = title,
                Content = content,
                Type = "custom",
                Icon = icon,
                IconColor = iconColor,
                TargetType = "user",
                TargetUserId = targetUserId,
                SentBy = "admin",
                SentByAdminId = AdminId,
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
                n.Id,
                n.Code,
                n.Title,
                n.Content,
                n.Type,
                n.Icon,
                n.IconColor,
                n.IsRead,
                n.ViolationContent,
                n.ViolationReason,
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

    // ========================================================================
    // KHU VỰC 27: CẤU HÌNH AI (AI SETTINGS) — chỉ superadmin
    // Bảng: AiSettings, AILogs
    // ========================================================================
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

        s.Model = string.IsNullOrWhiteSpace(form.Model) ? "models/gemini-2.5-flash" : form.Model.Trim();
        s.ApiVersion = string.IsNullOrWhiteSpace(form.ApiVersion) ? "v1beta" : form.ApiVersion.Trim();
        var tempStr = Request.Form["Temperature"].ToString();
        var temp = double.TryParse(tempStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var tp) ? tp : form.Temperature;
        s.Temperature = temp < 0 ? 0 : (temp > 2 ? 2 : temp);
        s.MaxOutputTokens = form.MaxOutputTokens > 0 ? form.MaxOutputTokens : 2048;
        s.ThinkingBudget = form.ThinkingBudget < 0 ? 0 : form.ThinkingBudget;
        s.SystemInstruction = form.SystemInstruction ?? "";
        s.SummarizeLength = string.IsNullOrWhiteSpace(form.SummarizeLength) ? "150-200 từ" : form.SummarizeLength.Trim();
        // Template trống -> giữ mặc định để AI không hỏng
        s.SummarizeTemplate = string.IsNullOrWhiteSpace(form.SummarizeTemplate) ? AiDefaults.Summarize : form.SummarizeTemplate;
        s.SuggestTitleTemplate = string.IsNullOrWhiteSpace(form.SuggestTitleTemplate) ? AiDefaults.SuggestTitle : form.SuggestTitleTemplate;
        s.ModerateTemplate = string.IsNullOrWhiteSpace(form.ModerateTemplate) ? AiDefaults.Moderate : form.ModerateTemplate;
        s.ChatMaxSentences = form.ChatMaxSentences > 0 ? form.ChatMaxSentences : 4;
        s.ChatTemplate = string.IsNullOrWhiteSpace(form.ChatTemplate) ? AiDefaults.Chat : form.ChatTemplate;
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

    // ========================================================================
    // KHU VỰC 28: HỒ SƠ NHÂN VIÊN CHI TIẾT (GĐ1)
    // Bảng: Admins, StaffProfiles, Articles, StaffActivityLogs
    // ========================================================================
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

    // ========================================================================
    // KHU VỰC 29: NHẬT KÝ HOẠT ĐỘNG (GĐ3) — Super Admin xem + xóa
    // Bảng: StaffActivityLogs
    // ========================================================================
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

    // ========================================================================
    // KHU VỰC 30: SLOT QUẢNG CÁO — CRUD + tìm kiếm
    // Bảng: AdSlots
    // ========================================================================
    // Đây là luồng xử lý danh sách slot quảng cáo (tìm kiếm theo tên/mã/kích thước)
    public async Task<IActionResult> AdSlots(string? q)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var query = _db.AdSlots.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var kw = q.Trim();
            query = query.Where(s => s.Name.Contains(kw) || s.SlotKey.Contains(kw) || s.Size.Contains(kw));
        }
        ViewBag.Q = q;
        // Slug 1 bài mẫu để nút "Xem trên trang" mở đúng trang bài cho các slot ở trang chi tiết
        ViewBag.SampleArticleSlug = await _db.Articles
            .Where(a => a.Status == "published")
            .OrderByDescending(a => a.PublishedAt)
            .Select(a => a.Slug).FirstOrDefaultAsync();
        return View(await query.OrderBy(s => s.Id).ToListAsync());
    }

    // ========================================================================
    // KHU VỰC 31: BẢNG ĐIỀU KHIỂN BỐ CỤC QUẢNG CÁO (preview + sắp thứ tự chạy)
    // Bảng: AdSlots, Advertisements, AdZoneSettings
    // ========================================================================
    // Đây là luồng xử lý mở bảng điều khiển bố cục quảng cáo
    // Luồng: lấy mọi khu (AdSlot, SlotKey=zone) -> mỗi khu gom TẤT CẢ QC gắn khu đó (mọi trạng thái)
    //        sắp theo DisplayOrder -> kèm chu kỳ nhảy (AdZoneSettings) -> đổ ra mockup preview.
    public async Task<IActionResult> AdLayout()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var slots = await _db.AdSlots.OrderBy(s => s.Id).ToListAsync();
        var settings = await _db.AdZoneSettings.ToListAsync();
        // Lấy quảng cáo CHƯA xóa mềm (kể cả QC tự thêm không qua slot) — gom theo Position/khu.
        var ads = await _db.Advertisements
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.DisplayOrder).ThenBy(a => a.Id).ToListAsync();

        var zones = slots.Select(s => new AdLayoutZoneVm
        {
            Position = s.SlotKey,
            Name = s.Name,
            Size = s.Size,
            RotationSeconds = settings.FirstOrDefault(z => z.Position == s.SlotKey)?.RotationSeconds ?? 5,
            Ads = ads.Where(a => a.Position == s.SlotKey).ToList()
        }).ToList();
        return View(zones);
    }

    // Đây là luồng xử lý lưu bố cục quảng cáo (AJAX)
    // Luồng: nhận DTO {khu -> thứ tự adIds + chu kỳ} -> gán DisplayOrder theo index,
    //        cập nhật/ tạo AdZoneSetting cho từng khu -> lưu. Có hiệu lực ngay lần tải kế.
    // Bảng: Advertisements, AdZoneSettings
    [HttpPost]
    public async Task<IActionResult> SaveAdLayout([FromBody] AdLayoutSaveDto dto)
    {
        if (!IsLoggedIn) return Json(new { success = false, message = "Chưa đăng nhập." });
        if (dto?.Zones == null) return Json(new { success = false, message = "Dữ liệu rỗng." });

        foreach (var z in dto.Zones)
        {
            // 1) thứ tự QC trong khu
            for (int i = 0; i < z.AdIds.Count; i++)
            {
                var ad = await _db.Advertisements.FindAsync(z.AdIds[i]);
                if (ad != null && ad.Position == z.Position) ad.DisplayOrder = i;
            }
            // 2) chu kỳ nhảy của khu
            var sec = z.RotationSeconds < 1 ? 1 : z.RotationSeconds;
            var setting = await _db.AdZoneSettings.FirstOrDefaultAsync(s => s.Position == z.Position);
            if (setting == null) _db.AdZoneSettings.Add(new AdZoneSetting { Position = z.Position, RotationSeconds = sec });
            else setting.RotationSeconds = sec;
        }
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Đây là luồng xử lý hiển thị form tạo slot quảng cáo
    public IActionResult CreateAdSlot()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        return View(new AdSlot { IsActive = true });
    }

    // Đây là luồng xử lý tạo slot quảng cáo mới (chống trùng mã vị trí)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAdSlot(AdSlot form)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        form.Name = (form.Name ?? "").Trim();
        form.SlotKey = (form.SlotKey ?? "").Trim();
        form.Size = (form.Size ?? "").Trim();
        if (string.IsNullOrEmpty(form.Name) || string.IsNullOrEmpty(form.SlotKey))
        { ViewBag.Error = "Vui lòng nhập Tên và Mã vị trí."; return View(form); }
        if (form.PricePerDay < 0) { ViewBag.Error = "Giá không được âm."; return View(form); }
        if (await _db.AdSlots.AnyAsync(s => s.SlotKey == form.SlotKey))
        { ViewBag.Error = "Mã vị trí đã tồn tại."; return View(form); }
        form.CreatedAt = DateTime.Now;
        _db.AdSlots.Add(form);
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã tạo slot quảng cáo.";
        return RedirectToAction("AdSlots");
    }

    // Đây là luồng xử lý hiển thị form sửa slot quảng cáo
    public async Task<IActionResult> EditAdSlot(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var slot = await _db.AdSlots.FindAsync(id);
        if (slot == null) return RedirectToAction("AdSlots");
        return View(slot);
    }

    // Đây là luồng xử lý cập nhật slot quảng cáo
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAdSlot(int id, AdSlot form)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var slot = await _db.AdSlots.FindAsync(id);
        if (slot == null) return RedirectToAction("AdSlots");
        form.Id = id;
        form.Name = (form.Name ?? "").Trim();
        form.SlotKey = (form.SlotKey ?? "").Trim();
        form.Size = (form.Size ?? "").Trim();
        if (string.IsNullOrEmpty(form.Name) || string.IsNullOrEmpty(form.SlotKey))
        { ViewBag.Error = "Vui lòng nhập Tên và Mã vị trí."; return View(form); }
        if (await _db.AdSlots.AnyAsync(s => s.SlotKey == form.SlotKey && s.Id != id))
        { ViewBag.Error = "Mã vị trí đã tồn tại."; return View(form); }
        slot.Name = form.Name;
        slot.SlotKey = form.SlotKey;
        slot.Description = form.Description;
        slot.Size = form.Size;
        slot.PricePerDay = form.PricePerDay < 0 ? 0 : form.PricePerDay;
        slot.IsActive = form.IsActive;
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Đã cập nhật slot.";
        return RedirectToAction("AdSlots");
    }

    // Đây là luồng xử lý xóa slot quảng cáo
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAdSlot(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var slot = await _db.AdSlots.FindAsync(id);
        if (slot != null) { _db.AdSlots.Remove(slot); await _db.SaveChangesAsync(); }
        return Json(new { success = true });
    }

    // ========================================================================
    // KHU VỰC 32: QUẢNG CÁO (GĐ1) — duyệt / từ chối / xác nhận thanh toán
    // Bảng: Advertisements, Transactions, Users, Admins
    // ========================================================================
    public async Task<IActionResult> Advertisements(string? filter)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        // CHỈ lấy quảng cáo CHƯA xóa cho danh sách chính
        var query = _db.Advertisements.Where(a => !a.IsDeleted);
        if (filter == "pending") query = query.Where(a => a.Status == "pending");
        else if (filter == "active") query = query.Where(a => a.Status == "approved" && a.IsActive);
        else if (filter == "paused") query = query.Where(a => a.Status == "approved" && !a.IsActive);
        else if (!string.IsNullOrEmpty(filter) && filter != "all") query = query.Where(a => a.Position == filter);
        var ads = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();

        ViewBag.Filter = filter ?? "all";
        ViewBag.PendingCount = await _db.Advertisements.CountAsync(a => !a.IsDeleted && a.Status == "pending");
        ViewBag.TotalImpressions = (await _db.Advertisements.Where(a => !a.IsDeleted).SumAsync(a => (int?)a.Impressions)) ?? 0;
        ViewBag.TotalClicks = (await _db.Advertisements.Where(a => !a.IsDeleted).SumAsync(a => (int?)a.Clicks)) ?? 0;
        // Thùng rác: các QC đã xóa mềm
        ViewBag.DeletedAds = await _db.Advertisements.Where(a => a.IsDeleted).OrderByDescending(a => a.DeletedAt).ToListAsync();
        // Map mã vị trí -> tên tiếng Việt (đủ mọi slot hiện có) + danh sách vị trí để lọc
        ViewBag.SlotNames = await _db.AdSlots.ToDictionaryAsync(s => s.SlotKey, s => s.Name);
        ViewBag.SlotList = await _db.AdSlots.OrderBy(s => s.Id).ToListAsync();
        return View(ads);
    }

    public async Task<IActionResult> CreateAd()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        ViewBag.SlotList = await _db.AdSlots.OrderBy(s => s.Id).ToListAsync();
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
        var validPos = await _db.AdSlots.Select(s => s.SlotKey).ToListAsync();
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
        ViewBag.SlotList = await _db.AdSlots.OrderBy(s => s.Id).ToListAsync();
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
        var validPos = await _db.AdSlots.Select(s => s.SlotKey).ToListAsync();
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
    // Đây là luồng xử lý xác nhận đã nhận thanh toán đơn quảng cáo
    // Luồng: 1) đánh dấu PaymentStatus="paid" + Transaction=Success
    //        2) tra email người mua (Nhà báo: bảng Users / Admin: bảng Admins) rồi gửi mail biên nhận
    // Bảng: Advertisements, Transactions, Users, Admins
    public async Task<IActionResult> ConfirmAdPayment(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var ad = await _db.Advertisements.Include(a => a.AdSlot).FirstOrDefaultAsync(a => a.Id == id);
        if (ad == null) return Json(new { success = false });
        ad.PaymentStatus = "paid";
        var tx = await _db.Transactions.FirstOrDefaultAsync(t => t.AdvertisementId == id && t.Status == TransactionStatus.Pending);
        if (tx != null) { tx.Status = TransactionStatus.Success; tx.UpdatedAt = DateTime.Now; }
        await _db.SaveChangesAsync();

        // Gửi mail biên nhận — báo rõ kết quả để biết vì sao nếu thất bại
        var (emailSent, emailMsg) = await SendAdReceiptAsync(ad);
        return Json(new { success = true, emailSent, emailMsg });
    }

    // Gửi email biên nhận thanh toán QC. Trả (đã gửi?, thông báo lý do). Dùng chung.
    // Bảng: Users, Admins
    private async Task<(bool sent, string msg)> SendAdReceiptAsync(Advertisement ad)
    {
        string? email = null, name = ad.CreatedByName;
        if (ad.CreatedByUserId != null)
        {
            var u = await _db.Users.FindAsync(ad.CreatedByUserId.Value);
            if (u != null) { email = u.Email; name = u.FullName; }
        }
        else if (ad.CreatedByAdminId != null)
        {
            var ad2 = await _db.Admins.FindAsync(ad.CreatedByAdminId.Value);
            if (ad2 != null) { email = ad2.Email; name = ad2.FullName; }
        }
        if (string.IsNullOrWhiteSpace(email)) return (false, "Không tìm thấy email người mua.");
        if (!_email.IsConfigured) return (false, "SMTP chưa cấu hình trong appsettings.json.");

        var body = $@"<div style='font-family:Arial,sans-serif;max-width:560px;margin:auto;'>
<h2 style='color:#0e7d85;'>Đã nhận thanh toán quảng cáo</h2>
<p>Xin chào <b>{name}</b>,</p>
<p>Chúng tôi đã <b>xác nhận thanh toán</b> cho đơn quảng cáo <b>QC{ad.Id}</b> — vị trí {ad.AdSlot?.Name} ({ad.Position}), {ad.Days} ngày, số tiền <b>{ad.Amount:#,##0} đ</b>, chạy {ad.StartDate:dd/MM/yyyy} → {ad.EndDate:dd/MM/yyyy}.</p>
<p style='color:#475569;'>Quảng cáo sẽ hiển thị sau khi được duyệt nội dung. Cảm ơn bạn đã tin dùng Wisdom IT News.</p></div>";
        var (ok, err) = await _email.SendAsync(email!, $"Biên nhận thanh toán quảng cáo QC{ad.Id} — Wisdom IT News", body, name);
        return ok ? (true, "Đã gửi email tới " + email) : (false, "Gửi email thất bại: " + err);
    }

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
        // XÓA MỀM: đưa vào thùng rác, tắt hiển thị; có thể khôi phục lại.
        ad.IsDeleted = true;
        ad.DeletedAt = DateTime.Now;
        ad.IsActive = false;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Khôi phục quảng cáo từ thùng rác
    [HttpPost]
    public async Task<IActionResult> RestoreAd(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var ad = await _db.Advertisements.FindAsync(id);
        if (ad == null) return Json(new { success = false });
        ad.IsDeleted = false;
        ad.DeletedAt = null;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Xóa VĨNH VIỄN (chỉ với QC đã ở trong thùng rác)
    [HttpPost]
    public async Task<IActionResult> PurgeAd(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var ad = await _db.Advertisements.FindAsync(id);
        if (ad == null) return Json(new { success = false });
        if (!ad.IsDeleted) return Json(new { success = false, message = "Chỉ xóa vĩnh viễn quảng cáo trong thùng rác." });
        _db.Advertisements.Remove(ad);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ========================================================================
    // ĐƠN ĐĂNG KÝ QUẢNG CÁO (AdBooking) — admin/nhân viên duyệt đơn của khách,
    // xác nhận (gửi mail), rồi ĐƯA quảng cáo lên trang (tạo Advertisement thật).
    // ========================================================================
    public async Task<IActionResult> AdBookings()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var bookings = await _db.AdBookings.OrderByDescending(b => b.CreatedAt).ToListAsync();
        ViewBag.SlotNames = await _db.AdSlots.ToDictionaryAsync(s => s.SlotKey, s => s.Name);
        return View(bookings);
    }

    // Xác nhận đơn + GỬI MAIL cho khách
    [HttpPost]
    public async Task<IActionResult> ConfirmAdBooking(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var b = await _db.AdBookings.FindAsync(id);
        if (b == null) return Json(new { success = false });
        b.Status = "AwaitingPayment";
        await _db.SaveChangesAsync();

        bool emailSent = false; string? emailMsg = null;
        try
        {
            if (_email.IsConfigured && !string.IsNullOrWhiteSpace(b.Email))
            {
                var html = $@"<div style='font-family:Arial,sans-serif;max-width:560px;margin:auto;'>
<h2 style='color:#0e7d85;'>Đơn quảng cáo #{b.Id} đã được xác nhận</h2>
<p>Xin chào <b>{System.Net.WebUtility.HtmlEncode(b.ContactName)}</b>,</p>
<p>Đơn đăng ký quảng cáo của bạn tại <b>Báo Online WisdomITNews</b> đã được <b>xác nhận</b>. Sau khi nhận thanh toán, chúng tôi sẽ đưa banner của bạn lên trang theo vị trí đã đăng ký.</p>
<p style='color:#475569;'>Cảm ơn bạn đã tin dùng WisdomITNews.</p></div>";
                var (ok, err) = await _email.SendAsync(b.Email, $"[WisdomITNews] Xác nhận đơn quảng cáo #{b.Id}", html, b.ContactName);
                emailSent = ok; emailMsg = err;
            }
            else emailMsg = "SMTP chưa cấu hình";
        }
        catch (Exception ex) { emailMsg = ex.Message; _logger.LogWarning(ex, "Gửi mail xác nhận đơn QC #{Id} lỗi", b.Id); }

        return Json(new { success = true, emailSent, emailMsg });
    }

    // Hủy đơn
    [HttpPost]
    public async Task<IActionResult> CancelAdBooking(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var b = await _db.AdBookings.FindAsync(id);
        if (b == null) return Json(new { success = false });
        b.Status = "Rejected";
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Đặt trạng thái theo quy trình (admin bấm thanh trạng thái)
    [HttpPost]
    public async Task<IActionResult> SetBookingStatus(int id, string status)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var allowed = new[] { "PendingConfirmation", "AwaitingPayment", "AwaitingContent", "UnderReview", "Scheduled", "Live", "Completed", "Rejected" };
        if (!allowed.Contains(status)) return Json(new { success = false, message = "Trạng thái không hợp lệ." });
        var b = await _db.AdBookings.FindAsync(id);
        if (b == null) return Json(new { success = false });
        b.Status = status;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Lưu ghi chú nội bộ cho đơn
    [HttpPost]
    public async Task<IActionResult> SaveBookingNote(int id, string? note)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var b = await _db.AdBookings.FindAsync(id);
        if (b == null) return Json(new { success = false });
        b.AdminNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // ĐƯA LÊN TRANG: từ đơn -> tạo Advertisement thật.
    //  adType = "image": tải banner ảnh + link đích.
    //  adType = "html" : dán mã HTML/JS hoặc upload file .html (chạy trong iframe sandbox).
    [HttpPost]
    public async Task<IActionResult> PublishAdBooking(int id, string? adType, string? targetUrl,
        IFormFile? bannerFile, string? htmlContent, IFormFile? htmlFile)
    {
        if (!IsLoggedIn) { TempData["Ok"] = "Chưa đăng nhập."; return RedirectToAction("AdBookings"); }
        var b = await _db.AdBookings.FindAsync(id);
        if (b == null) return RedirectToAction("AdBookings");

        adType = adType == "html" ? "html" : "image";
        string? img = null, html = null;

        if (adType == "html")
        {
            // Ưu tiên file .html tải lên; không có thì lấy phần dán trong ô mã
            if (htmlFile != null && htmlFile.Length > 0)
            {
                using var reader = new StreamReader(htmlFile.OpenReadStream());
                html = await reader.ReadToEndAsync();
            }
            else html = htmlContent;
            if (string.IsNullOrWhiteSpace(html)) { TempData["Ok"] = "⚠ Vui lòng dán mã HTML/JS hoặc chọn file .html."; return RedirectToAction("AdBookings"); }
        }
        else
        {
            if (bannerFile != null && bannerFile.Length > 0)
            {
                var up = await _imageUpload.SaveAsync(bannerFile);
                if (up.Success) img = up.RelativePath;
            }
            if (string.IsNullOrEmpty(img)) { TempData["Ok"] = "⚠ Vui lòng chọn ảnh banner để đưa lên trang."; return RedirectToAction("AdBookings"); }
        }

        var now = DateTime.Now;
        var slot = await _db.AdSlots.FirstOrDefaultAsync(s => s.SlotKey == b.AdPosition);
        _db.Advertisements.Add(new Advertisement
        {
            Title = (string.IsNullOrWhiteSpace(b.CompanyName) ? b.ContactName : b.CompanyName) + $" — Đơn #{b.Id}",
            AdType = adType,
            ImageUrl = img,
            HtmlContent = html,
            TargetUrl = string.IsNullOrWhiteSpace(targetUrl) ? (b.Website ?? "#") : targetUrl.Trim(),
            Position = b.AdPosition,
            AdSlotId = slot?.Id,
            Days = b.DurationDays,
            Amount = b.Amount,
            StartDate = now,
            EndDate = now.AddDays(b.DurationDays <= 0 ? 7 : b.DurationDays),
            IsActive = true,
            Status = "approved",
            PaymentStatus = "paid",
            CreatedByAdminId = AdminId,
            CreatedByName = (HttpContext.Session.GetString("AdminName") ?? "Admin") + $" (đơn #{b.Id})",
            BuyerPhone = b.Phone,
            CreatedAt = now
        });
        b.Status = "Live";
        await _db.SaveChangesAsync();

        try
        {
            if (_email.IsConfigured && !string.IsNullOrWhiteSpace(b.Email))
            {
                var emailHtml = $@"<div style='font-family:Arial,sans-serif;max-width:560px;margin:auto;'>
<h2 style='color:#16a34a;'>Quảng cáo của bạn đã lên sóng 🎉</h2>
<p>Xin chào <b>{System.Net.WebUtility.HtmlEncode(b.ContactName)}</b>, banner quảng cáo (đơn #{b.Id}) đã được đưa lên trang tại vị trí <b>{System.Net.WebUtility.HtmlEncode(slot?.Name ?? b.AdPosition)}</b>, chạy {b.DurationDays} ngày.</p></div>";
                await _email.SendAsync(b.Email, $"[WisdomITNews] Quảng cáo đơn #{b.Id} đã lên sóng", emailHtml, b.ContactName);
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Gửi mail lên sóng đơn QC #{Id} lỗi", b.Id); }

        TempData["Ok"] = $"Đã đưa quảng cáo của đơn #{b.Id} lên trang.";
        return RedirectToAction("AdBookings");
    }

    // ========================================================================
    // KHU VỰC 33: PODCAST / AUDIO
    // Bảng: Podcasts
    // ========================================================================
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