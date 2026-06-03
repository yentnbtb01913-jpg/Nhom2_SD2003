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
    private readonly ILogger<AdminController> _logger;

    private readonly EmailService _email;

    public AdminController(
        AppDbContext db,
        ImageUploadService imageUpload,
        AIService ai,
        EmailService email,
        ILogger<AdminController> logger)
    {
        _db = db;
        _imageUpload = imageUpload;
        _ai = ai;
        _email = email;
        _logger = logger;
    }

    private bool IsLoggedIn => HttpContext.Session.GetString("AdminId") != null;
    private int AdminId => int.Parse(HttpContext.Session.GetString("AdminId") ?? "0");

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
        return RedirectToAction("Dashboard");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
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
        var query = _db.Articles.Include(a => a.Category).Include(a => a.Author).Include(a => a.AuthorUser).AsQueryable();
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

    // ===== CATEGORIES =====
    public async Task<IActionResult> Categories()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        return View(await _db.Categories.OrderBy(c => c.SortOrder).ToListAsync());
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
    public async Task<IActionResult> Newsletter()
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
        var subs = await _db.NewsletterSubscribers
            .OrderByDescending(n => n.SubscribedAt)
            .Take(500)
            .ToListAsync();
        ViewBag.SmtpConfigured = _email.IsConfigured;
        return View(subs);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendNewsletter(string subject, string htmlBody)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
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
        var s = await _db.NewsletterSubscribers.FindAsync(id);
        if (s == null) return Json(new { success = false });
        _db.NewsletterSubscribers.Remove(s);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
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
        var users = await _db.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
        return View(users);
    }

    [HttpGet]
    public async Task<IActionResult> UserDetail(int id)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");
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
    public async Task<IActionResult> AdminUploadUserAvatar(int userId, IFormFile avatarFile)
    {
        if (!IsLoggedIn) return Json(new { success = false });
        if (avatarFile == null || avatarFile.Length == 0)
            return Json(new { success = false, message = "Vui lòng chọn ảnh" });

        try
        {
            var up = await _imageUpload.SaveAsync(avatarFile);
            if (!up.Success) return Json(new { success = false, message = up.Error });

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return Json(new { success = false, message = "Không tìm thấy user" });

            user.AvatarUrl = up.RelativePath;
            await _db.SaveChangesAsync();
            return Json(new { success = true, avatarUrl = user.AvatarUrl });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AdminUploadUserAvatar failed");
            return Json(new { success = false, message = "Lỗi hệ thống" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> DeleteUser(int id)
    {
        if (!IsLoggedIn) return Json(new { success = false });
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
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(string username, string email, string password, string fullName, string role)
    {
        if (!IsLoggedIn) return RedirectToAction("Login");

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

        var member = await _db.ChatMembers.FindAsync(id);
        if (member == null) return Json(new { success = false, message = "Không tìm thấy thành viên" });

        _db.ChatMembers.Remove(member);
        await _db.SaveChangesAsync();

        return Json(new { success = true });
    }


    // ===== HELPER =====
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
}