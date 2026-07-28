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
    private readonly ExternalArticleService _external;
    private readonly FeaturedArticleService _featuredSvc;
    private readonly EmailService _email;

    public NhanVienController(AppDbContext db, ImageUploadService imageUpload, AIService ai, VideoUploadService videoUpload, ILogger<NhanVienController> logger, IServiceProvider serviceProvider, ExternalArticleService external, FeaturedArticleService featuredSvc, EmailService email)
    {
        _db = db;
        _imageUpload = imageUpload;
        _ai = ai;
        _videoUpload = videoUpload;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _external = external;
        _featuredSvc = featuredSvc;
        _email = email;
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
            var actor = HttpContext.Session.GetString("AdminName") ?? "Nhân viên";
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

    // ===== CATEGORIES (CRUD + tìm kiếm) — đồng bộ với Admin =====
    // Đây là luồng xử lý danh sách danh mục (kèm số bài mỗi danh mục)
    // Bảng: Categories, Articles
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

    // Đây là luồng xử lý tạo danh mục mới (sinh slug, chống trùng)
    // Bảng: Categories
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
        await TryLogStaffAsync("category_create", $"Thêm danh mục: {cat.Name}", cat.Id);
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

    // Đây là luồng xử lý cập nhật danh mục
    // Bảng: Categories
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
        await TryLogStaffAsync("category_edit", $"Sửa danh mục: {cat.Name}", cat.Id);
        TempData["Ok"] = "Đã cập nhật danh mục.";
        return RedirectToAction("Categories");
    }

    // Đây là luồng xử lý bật/tắt hiển thị danh mục (IsVisible)
    // Bảng: Categories
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

    // Đây là luồng xử lý xóa danh mục (chặn nếu còn bài viết / danh mục con)
    // Bảng: Categories, Articles
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
        await TryLogStaffAsync("category_delete", $"Xóa danh mục: {cat.Name}", id);
        return Json(new { success = true });
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

    // ===== ĐỐI TÁC (NHÀ BÁO) — đồng bộ với Admin (lọc/tìm + CRUD) =====
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

    // Đây là luồng xử lý hiển thị form thêm nhà báo
    [HttpGet]
    public async Task<IActionResult> CreateJournalist()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        ViewBag.Categories = await _db.Categories.OrderBy(c => c.SortOrder).ToListAsync();
        ViewBag.Profile = new JournalistProfile();
        return View();
    }

    // Đây là luồng xử lý tạo nhà báo mới (User Role=Journalist + JournalistProfile + upload avatar)
    // Bảng: Users, JournalistProfiles
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
        await TryLogStaffAsync("journalist_create", $"Thêm nhà báo: {user.FullName}", user.Id);
        TempData["Ok"] = "Đã thêm nhà báo mới.";
        return RedirectToAction("Partners");
    }

    // Đây là luồng xử lý hiển thị form sửa nhà báo
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

    // Đây là luồng xử lý cập nhật thông tin nhà báo (+ hồ sơ)
    // Bảng: Users, JournalistProfiles
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
        await TryLogStaffAsync("journalist_edit", $"Sửa nhà báo: {user.FullName}", user.Id);
        TempData["Ok"] = "Đã cập nhật hồ sơ nhà báo.";
        return RedirectToAction("Partners");
    }

    // Đây là luồng xử lý xem chi tiết hồ sơ nhà báo
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

    // Đây là luồng xử lý xóa mềm nhà báo (IsDeleted=true, giữ dữ liệu)
    [HttpPost]
    public async Task<IActionResult> SoftDeleteJournalist(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var u = await _db.Users.FindAsync(id);
        if (u == null || u.Role != "Journalist") return Json(new { success = false });
        u.IsDeleted = true; await _db.SaveChangesAsync();
        await TryLogStaffAsync("journalist_softdelete", $"Xóa mềm nhà báo #{id}", id);
        return Json(new { success = true });
    }

    // Đây là luồng xử lý khôi phục nhà báo đã xóa mềm
    [HttpPost]
    public async Task<IActionResult> RestoreJournalist(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var u = await _db.Users.FindAsync(id);
        if (u == null || u.Role != "Journalist") return Json(new { success = false });
        u.IsDeleted = false; await _db.SaveChangesAsync();
        await TryLogStaffAsync("journalist_restore", $"Khôi phục nhà báo #{id}", id);
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

    // Đây là luồng xử lý hiển thị form sửa video (đồng bộ với Admin)
    public async Task<IActionResult> EditVideo(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var video = await _db.Videos.FindAsync(id);
        if (video == null) return RedirectToAction("Videos");
        ViewBag.Video = video;
        return View();
    }

    // Đây là luồng xử lý cập nhật video (YouTube id / upload file)
    // Bảng: Videos
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(VideoUploadService.MaxSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = VideoUploadService.MaxSize)]
    public async Task<IActionResult> EditVideo(int id, string videoType, string? youtubeUrl, string title,
        string? source, string? description, string status = "published", IFormFile? videoFile = null)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var video = await _db.Videos.FindAsync(id);
        if (video == null) return RedirectToAction("Videos");

        if (string.IsNullOrWhiteSpace(title))
        {
            ViewBag.Video = video; ViewBag.Error = "Vui lòng nhập tiêu đề."; return View();
        }

        if (videoType == "upload")
        {
            if (videoFile != null && videoFile.Length > 0)
            {
                if (video.VideoType == "upload" && !string.IsNullOrEmpty(video.VideoUrl))
                    _videoUpload.DeletePhysicalFile(video.VideoUrl);
                var upload = await _videoUpload.SaveAsync(videoFile);
                if (!upload.Success)
                {
                    ViewBag.Video = video; ViewBag.Error = upload.Error ?? "Lỗi upload video."; return View();
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
                ViewBag.Video = video; ViewBag.Error = "Vui lòng nhập link YouTube hợp lệ."; return View();
            }
            if (video.VideoType == "upload" && !string.IsNullOrEmpty(video.VideoUrl))
            {
                _videoUpload.DeletePhysicalFile(video.VideoUrl);
                video.VideoUrl = null; video.FileSize = null;
            }
            video.YouTubeId = vid;
            video.VideoType = "youtube";
        }

        video.Title = title.Trim();
        video.Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim();
        video.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        video.Status = status == "draft" ? "draft" : "published";
        await _db.SaveChangesAsync();
        await TryLogStaffAsync("video_edit", $"Sửa video: {video.Title}", video.Id);
        TempData["Ok"] = "Đã cập nhật video.";
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

    // ===== QUẢN LÝ NGUỒN RSS (đồng bộ với Admin) =====
    // Đây là luồng xử lý thêm nguồn RSS mới
    // Bảng: RssSources
    [HttpPost]
    public async Task<IActionResult> AddRssSource(string name, string feedUrl, string? websiteUrl, string? description, string? country, int? defaultCategoryId, int maxImport = 30, string? sourceType = null)
    {
        if (!IsLoggedIn) return Json(new { success = false });
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
        await TryLogStaffAsync("rss_add", $"Thêm nguồn RSS: {name}");
        return Json(new { success = true });
    }

    // Đây là luồng xử lý bật/tắt một nguồn RSS
    // Bảng: RssSources
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

    // Đây là luồng xử lý xóa một nguồn RSS
    // Bảng: RssSources
    [HttpPost]
    public async Task<IActionResult> DeleteRssSource(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var src = await _db.RssSources.FindAsync(id);
        if (src == null) return Json(new { success = false });
        _db.RssSources.Remove(src);
        await _db.SaveChangesAsync();
        await TryLogStaffAsync("rss_delete", $"Xóa nguồn RSS #{id}");
        return Json(new { success = true });
    }

    // ===== KÊNH BÊN NGOÀI (trang riêng — chỉ bài IsExternal = true) — dùng chung ExternalArticleService với Admin =====

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
        var editor = HttpContext.Session.GetString("AdminName") ?? "Nhân viên";
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

    // ===== TIN NỔI BẬT TỰ ĐỘNG (Trang chủ) — tab quản lý chỉ xem + can thiệp Ghim/Loại =====

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

    // [ĐÃ GỠ] Khu Quản lý khách hàng (đăng ký nhận tin) đã được loại bỏ.
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
    // [ĐÃ GỠ] Khu Gia hạn quảng cáo (RenewalAds/AdChat/ExtendAdRenewal) đã được loại bỏ.

    // ===== QUẢNG CÁO (Nhân viên quản lý + duyệt) =====
    public async Task<IActionResult> Advertisements(string? filter)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var query = _db.Advertisements.AsQueryable();
        if (filter == "pending") query = query.Where(a => a.Status == "pending");
        else if (filter == "active") query = query.Where(a => a.Status == "approved" && a.IsActive);
        else if (filter == "header" || filter == "sidebar" || filter == "in_article" || filter == "home_left" || filter == "home_right") query = query.Where(a => a.Position == filter);
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
        var validPos = new[] { "header", "sidebar", "in_article", "home_left", "home_right" };
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
        var validPos = new[] { "header", "sidebar", "in_article", "home_left", "home_right" };
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

    // Đây là luồng xử lý xác nhận đã nhận thanh toán đơn quảng cáo (đồng bộ với Admin)
    // Luồng: đánh dấu paid + Transaction=Success -> gửi mail biên nhận cho người mua (best-effort)
    // Bảng: Advertisements, Transactions, Users, Admins
    [HttpPost]
    public async Task<IActionResult> ConfirmAdPayment(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        var ad = await _db.Advertisements.Include(a => a.AdSlot).FirstOrDefaultAsync(a => a.Id == id);
        if (ad == null) return Json(new { success = false });
        ad.PaymentStatus = "paid";
        var tx = await _db.Transactions.FirstOrDefaultAsync(t => t.AdvertisementId == id && t.Status == TransactionStatus.Pending);
        if (tx != null) { tx.Status = TransactionStatus.Success; tx.UpdatedAt = DateTime.Now; }
        await _db.SaveChangesAsync();
        await TryLogStaffAsync("ad_confirm_payment", "Xác nhận thanh toán QC #" + id, id);

        var (emailSent, emailMsg) = await SendAdReceiptAsync(ad);
        return Json(new { success = true, emailSent, emailMsg });
    }

    // Gửi email biên nhận thanh toán QC. Trả (đã gửi?, thông báo lý do).
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
    // [ĐÃ GỠ] Action TeamChat (Chat nội bộ) đã được loại bỏ.

    // [ĐÃ GỠ] Khu Quản lý khách hàng Premium đã được loại bỏ.
}
