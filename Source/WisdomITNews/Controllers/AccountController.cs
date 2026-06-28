using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace WisdomITNews.Controllers;

/// <summary>
/// [N] Đăng ký / đăng nhập / profile cho người đọc (User), tách biệt với Admin.
/// Session keys: UserId, UserName, UserAvatar.
/// </summary>
public class AccountController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<AccountController> _logger;

    public AccountController(AppDbContext db, ILogger<AccountController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private bool IsLoggedIn => HttpContext.Session.GetInt32("UserId") != null;

    // ===== REGISTER =====
    [HttpGet]
    public IActionResult Register()
    {
        if (IsLoggedIn) return RedirectToAction("Index", "Home");
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Error = string.Join(" • ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return View(vm);
        }

        try
        {
            var lowerUsername = vm.Username.Trim().ToLowerInvariant();
            var lowerEmail = vm.Email.Trim().ToLowerInvariant();

            if (await _db.Users.AnyAsync(u => u.Username == lowerUsername))
            {
                vm.Error = "Tên đăng nhập đã tồn tại";
                return View(vm);
            }
            if (await _db.Users.AnyAsync(u => u.Email == lowerEmail))
            {
                vm.Error = "Email đã được đăng ký";
                return View(vm);
            }

            var user = new User
            {
                Username = lowerUsername,
                Email = lowerEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.Password),
                FullName = vm.FullName.Trim(),
                Role = "Reader",
                IsEmailConfirmed = false,
                CreatedAt = DateTime.Now
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Auto login sau khi đăng ký
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.FullName);
            HttpContext.Session.SetString("UserAvatar", user.AvatarUrl ?? "");

            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Register failed");
            vm.Error = "Lỗi hệ thống. Thử lại sau.";
            return View(vm);
        }
    }

    // ===== LOGIN =====
    [HttpGet]
    public IActionResult Login()
    {
        if (IsLoggedIn) return RedirectToAction("Index", "Home");
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Error = string.Join(" • ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return View(vm);
        }

        try
        {
            var key = vm.UsernameOrEmail.Trim().ToLowerInvariant();
            var user = await _db.Users.FirstOrDefaultAsync(u =>
                u.Username == key || u.Email == key);

            if (user == null || !BCrypt.Net.BCrypt.Verify(vm.Password, user.PasswordHash))
            {
                vm.Error = "Sai tên đăng nhập / email hoặc mật khẩu";
                return View(vm);
            }

            if (!user.IsActive)
            {
                vm.Error = "Tài khoản đã bị khoá. Vui lòng liên hệ quản trị viên.";
                return View(vm);
            }

            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.FullName);
            HttpContext.Session.SetString("UserAvatar", user.AvatarUrl ?? "");

            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Login failed");
            vm.Error = "Lỗi hệ thống. Thử lại sau.";
            return View(vm);
        }
    }

    // ===== LOGOUT =====
    public IActionResult Logout()
    {
        HttpContext.Session.Remove("UserId");
        HttpContext.Session.Remove("UserName");
        HttpContext.Session.Remove("UserAvatar");
        return RedirectToAction("Index", "Home");
    }

    // ===== ĐĂNG NHẬP NGOÀI: GOOGLE / FACEBOOK =====
    // Bước 1: chuyển hướng người dùng sang nhà cung cấp
    [HttpGet]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return RedirectToAction("Login");

        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var props = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(props, provider); // provider = "Google" hoặc "Facebook"
    }

    // Bước 2: nhà cung cấp gọi lại — tìm/tạo user theo email rồi set session
    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (!string.IsNullOrEmpty(remoteError))
        {
            _logger.LogWarning("External login error: {Err}", remoteError);
            return RedirectToAction("Login");
        }

        var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = auth?.Principal;
        if (principal == null) return RedirectToAction("Login");

        var email = principal.FindFirstValue(ClaimTypes.Email);
        var fullName = principal.FindFirstValue(ClaimTypes.Name);
        var avatar = principal.FindFirstValue("picture");
        var providerKey = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        // Dọn cookie tạm của external (web dùng Session riêng)
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Provider có thể không trả email (vd Facebook chỉ xin public_profile).
        // Khi đó tạo email tổng hợp từ id để vẫn khớp/tạo tài khoản duy nhất.
        if (string.IsNullOrWhiteSpace(email))
        {
            if (string.IsNullOrWhiteSpace(providerKey))
                return RedirectToAction("Login");
            email = $"social_{providerKey}@noemail.local";
        }

        try
        {
            email = email.Trim().ToLowerInvariant();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user != null && !user.IsActive)
                return RedirectToAction("Login");

            if (user == null)
            {
                var local = email.Split('@')[0];
                var baseUsername = System.Text.RegularExpressions.Regex.Replace(local, "[^a-z0-9_.]", "");
                if (string.IsNullOrWhiteSpace(baseUsername)) baseUsername = "user";
                var username = baseUsername;
                int i = 1;
                while (await _db.Users.AnyAsync(u => u.Username == username))
                    username = baseUsername + (i++);

                user = new User
                {
                    Username = username,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                    FullName = string.IsNullOrWhiteSpace(fullName) ? username : fullName.Trim(),
                    AvatarUrl = string.IsNullOrWhiteSpace(avatar) ? null : avatar,
                    Role = "Reader",
                    IsActive = true,
                    IsEmailConfirmed = true,
                    CreatedAt = DateTime.Now
                };
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
            }

            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.FullName);
            HttpContext.Session.SetString("UserAvatar", user.AvatarUrl ?? "");

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ExternalLoginCallback failed");
            return RedirectToAction("Login");
        }
    }

    // ===== ĐĂNG NHẬP NHÂN VIÊN BẰNG GOOGLE (khu Wisdom Nhân viên) =====
    // Bước 1: chuyển sang Google; callback RIÊNG để map vào bảng Admin (không tạo tài khoản mới).
    [HttpGet]
    public IActionResult StaffExternalLogin(string provider = "Google")
    {
        if (string.IsNullOrWhiteSpace(provider)) provider = "Google";
        var redirectUrl = Url.Action(nameof(StaffExternalLoginCallback), "Account");
        var props = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(props, provider);
    }

    // Bước 2: Google gọi lại — khớp email với nhân viên ĐÃ được cấp (bảng Admin), set session admin.
    [HttpGet]
    public async Task<IActionResult> StaffExternalLoginCallback(string? remoteError = null)
    {
        if (!string.IsNullOrEmpty(remoteError))
        {
            TempData["StaffLoginError"] = "Đăng nhập Google thất bại. Vui lòng thử lại.";
            return RedirectToAction("Login", "NhanVien");
        }

        var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = auth?.Principal;
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (principal == null)
        {
            TempData["StaffLoginError"] = "Không lấy được thông tin từ Google.";
            return RedirectToAction("Login", "NhanVien");
        }

        var email = principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["StaffLoginError"] = "Tài khoản Google không có email.";
            return RedirectToAction("Login", "NhanVien");
        }

        email = email.Trim().ToLowerInvariant();
        // CHỈ khớp nhân viên đã có trong bảng Admin và đang hoạt động — KHÔNG tự tạo tài khoản.
        var admin = await _db.Admins.FirstOrDefaultAsync(a => a.Email != null && a.Email.ToLower() == email && a.IsActive);
        if (admin == null)
        {
            TempData["StaffLoginError"] = "Email này chưa được cấp quyền nhân viên. Liên hệ quản trị viên.";
            return RedirectToAction("Login", "NhanVien");
        }

        admin.LastLogin = DateTime.Now;
        await _db.SaveChangesAsync();
        HttpContext.Session.SetString("AdminId", admin.Id.ToString());
        HttpContext.Session.SetString("AdminName", admin.FullName);
        HttpContext.Session.SetString("AdminRole", admin.Role);
        return RedirectToAction("Dashboard", "NhanVien");
    }

    // ===== PROFILE — public =====
    [HttpGet]
    [Route("/u/{username}")]
    public async Task<IActionResult> Profile(string username)
    {
        if (string.IsNullOrEmpty(username)) return NotFound();
        var key = username.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == key);
        if (user == null) return NotFound();

        var currentUserId = HttpContext.Session.GetInt32("UserId");
        bool isOwn = currentUserId.HasValue && currentUserId.Value == user.Id;

        // Friend/Follow status
        string friendStatus = "none";
        bool iFollow = false;
        bool followsMe = false;

        if (currentUserId.HasValue && !isOwn)
        {
            var friendship = await _db.Friendships.FirstOrDefaultAsync(f =>
                (f.RequesterId == currentUserId && f.ReceiverId == user.Id) ||
                (f.RequesterId == user.Id && f.ReceiverId == currentUserId));

            if (friendship != null)
            {
                if (friendship.Status == "accepted") friendStatus = "friends";
                else if (friendship.Status == "pending" && friendship.RequesterId == currentUserId) friendStatus = "sent";
                else if (friendship.Status == "pending" && friendship.ReceiverId == currentUserId) friendStatus = "received";
            }

            iFollow = await _db.UserFollows.AnyAsync(f => f.FollowerId == currentUserId && f.FollowingId == user.Id);
            followsMe = await _db.UserFollows.AnyAsync(f => f.FollowerId == user.Id && f.FollowingId == currentUserId);
        }

        // Counts
        int friendCount = await _db.Friendships.CountAsync(f =>
            (f.RequesterId == user.Id || f.ReceiverId == user.Id) && f.Status == "accepted");
        int followerCount = await _db.UserFollows.CountAsync(f => f.FollowingId == user.Id);
        int followingCount = await _db.UserFollows.CountAsync(f => f.FollowerId == user.Id);

        var vm = new ProfileViewModel
        {
            User = user,
            Articles = await _db.Articles
                .Include(a => a.Category)
                .Where(a => a.AuthorUserId == user.Id && a.Status == "published")
                .OrderByDescending(a => a.PublishedAt)
                .Take(20)
                .ToListAsync(),
            CommentCount = await _db.Comments.CountAsync(c => c.UserId == user.Id),
            FriendStatus = friendStatus,
            IFollow = iFollow,
            FollowsMe = followsMe,
            FriendCount = friendCount,
            FollowerCount = followerCount,
            FollowingCount = followingCount,
            IsOwnProfile = isOwn
        };

        ViewData["Title"] = $"@{user.Username} — {user.FullName}";
        return View(vm);
    }

    // ===== MY PROFILE — private (yêu cầu đăng nhập) =====
    [HttpGet]
    public async Task<IActionResult> MyProfile()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login");

        var user = await _db.Users.FindAsync(userId.Value);
        if (user == null) return RedirectToAction("Login");

        var vm = new ProfileViewModel
        {
            User = user,
            Articles = await _db.Articles
                .Include(a => a.Category)
                .Where(a => a.AuthorUserId == user.Id)
                .OrderByDescending(a => a.PublishedAt)
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
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
            return Json(new { success = false, message = "Chưa đăng nhập" });

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
            return Json(new { success = false, message = "Vui lòng điền đầy đủ thông tin" });

        try
        {
            var user = await _db.Users.FindAsync(userId.Value);
            if (user == null)
                return Json(new { success = false, message = "Không tìm thấy tài khoản" });

            var lowerEmail = email.Trim().ToLowerInvariant();

            // Kiểm tra email trùng với user khác
            var emailExists = await _db.Users.AnyAsync(u => u.Email == lowerEmail && u.Id != user.Id);
            if (emailExists)
                return Json(new { success = false, message = "Email đã được sử dụng bởi tài khoản khác" });

            user.FullName = fullName.Trim();
            user.Email = lowerEmail;
            user.Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
            await _db.SaveChangesAsync();

            // Cập nhật session
            HttpContext.Session.SetString("UserName", user.FullName);

            return Json(new { success = true, message = "Cập nhật thành công!" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UpdateProfile failed for userId={UserId}", userId);
            return Json(new { success = false, message = "Lỗi hệ thống. Thử lại sau." });
        }
    }



    // ===== UPLOAD AVATAR =====
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAvatar(IFormFile avatarFile)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
            return Json(new { success = false, message = "Chưa đăng nhập" });

        if (avatarFile == null || avatarFile.Length == 0)
            return Json(new { success = false, message = "Vui lòng chọn ảnh" });

        if (avatarFile.Length > 5 * 1024 * 1024)
            return Json(new { success = false, message = "Ảnh quá lớn (tối đa 5MB)" });

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(avatarFile.ContentType.ToLower()))
            return Json(new { success = false, message = "Chỉ hỗ trợ JPG, PNG, GIF, WEBP" });

        try
        {
            var user = await _db.Users.FindAsync(userId.Value);
            if (user == null)
                return Json(new { success = false, message = "Không tìm thấy tài khoản" });

            // Tạo thư mục uploads/avatars nếu chưa có
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "avatars");
            Directory.CreateDirectory(uploadsDir);

            // Tạo tên file duy nhất
            var ext = Path.GetExtension(avatarFile.FileName).ToLower();
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            var fileName = $"avatar_{user.Id}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            // Xóa avatar cũ nếu có (file local)
            if (!string.IsNullOrEmpty(user.AvatarUrl) && user.AvatarUrl.StartsWith("/uploads/"))
            {
                var oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.AvatarUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);
            }

            // Lưu file mới
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await avatarFile.CopyToAsync(stream);
            }

            // Cập nhật database
            var avatarUrl = $"/uploads/avatars/{fileName}";
            user.AvatarUrl = avatarUrl;
            await _db.SaveChangesAsync();

            // Cập nhật session
            HttpContext.Session.SetString("UserAvatar", avatarUrl);

            return Json(new { success = true, message = "Cập nhật ảnh đại diện thành công!", avatarUrl });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UploadAvatar failed for userId={UserId}", userId);
            return Json(new { success = false, message = "Lỗi hệ thống. Thử lại sau." });
        }
    }

    // ===== UPLOAD COVER =====
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadCover(IFormFile coverFile)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
            return Json(new { success = false, message = "Chưa đăng nhập" });

        if (coverFile == null || coverFile.Length == 0)
            return Json(new { success = false, message = "Vui lòng chọn ảnh" });

        if (coverFile.Length > 10 * 1024 * 1024)
            return Json(new { success = false, message = "Ảnh quá lớn (tối đa 10MB)" });

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(coverFile.ContentType.ToLower()))
            return Json(new { success = false, message = "Chỉ hỗ trợ JPG, PNG, GIF, WEBP" });

        try
        {
            var user = await _db.Users.FindAsync(userId.Value);
            if (user == null)
                return Json(new { success = false, message = "Không tìm thấy tài khoản" });

            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "covers");
            Directory.CreateDirectory(uploadsDir);

            var ext = Path.GetExtension(coverFile.FileName).ToLower();
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            var fileName = $"cover_{user.Id}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            // Xóa cover cũ
            if (!string.IsNullOrEmpty(user.CoverUrl) && user.CoverUrl.StartsWith("/uploads/"))
            {
                var oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.CoverUrl.TrimStart('/'));
                if (System.IO.File.Exists(oldPath))
                    System.IO.File.Delete(oldPath);
            }

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await coverFile.CopyToAsync(stream);
            }

            var coverUrl = $"/uploads/covers/{fileName}";
            user.CoverUrl = coverUrl;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Cập nhật ảnh bìa thành công!", coverUrl });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UploadCover failed for userId={UserId}", userId);
            return Json(new { success = false, message = "Lỗi hệ thống. Thử lại sau." });
        }
    }

    // ===== FOLLOWER LIST =====
    [HttpGet]
    [Route("/u/{username}/followers")]
    public async Task<IActionResult> Followers(string username)
    {
        if (string.IsNullOrEmpty(username)) return NotFound();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username.Trim().ToLowerInvariant());
        if (user == null) return NotFound();

        var followers = await _db.UserFollows
            .Where(f => f.FollowingId == user.Id)
            .Join(_db.Users, f => f.FollowerId, u => u.Id, (f, u) => u)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        ViewBag.ProfileUser = user;
        ViewBag.ListType = "Người theo dõi";
        return View("FollowList", followers);
    }

    [HttpGet]
    [Route("/u/{username}/following")]
    public async Task<IActionResult> Following(string username)
    {
        if (string.IsNullOrEmpty(username)) return NotFound();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username.Trim().ToLowerInvariant());
        if (user == null) return NotFound();

        var following = await _db.UserFollows
            .Where(f => f.FollowerId == user.Id)
            .Join(_db.Users, f => f.FollowingId, u => u.Id, (f, u) => u)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        ViewBag.ProfileUser = user;
        ViewBag.ListType = "Đang theo dõi";
        return View("FollowList", following);
    }

    // ===================== KHU CÁ NHÂN (HUB) =====================
    // 1 action, 5 tab: foryou / topics / following / history / saved
    [HttpGet]
    public async Task<IActionResult> Hub(string tab = "foryou")
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login");
        var user = await _db.Users.FindAsync(userId.Value);
        if (user == null) return RedirectToAction("Login");

        ViewBag.User = user;
        ViewBag.Tab = tab;
        ViewBag.SavedCount  = await _db.SavedArticles.CountAsync(x => x.UserId == user.Id);
        ViewBag.FollowCount = await _db.UserFollows.CountAsync(f => f.FollowerId == user.Id);
        ViewBag.TopicCount  = await _db.UserCategoryFollows.CountAsync(f => f.UserId == user.Id);

        if (tab == "saved")
        {
            var ids = await _db.SavedArticles.Where(x => x.UserId == user.Id)
                .OrderByDescending(x => x.SavedAt).Select(x => x.ArticleId).ToListAsync();
            var arts = await _db.Articles.Include(a => a.Category).Where(a => ids.Contains(a.Id)).ToListAsync();
            ViewBag.Articles = ids.Select(id => arts.FirstOrDefault(a => a.Id == id)).Where(a => a != null).Cast<Article>().ToList();
        }
        else if (tab == "history")
        {
            var sid = HttpContext.Session.Id;
            var ids = await _db.ViewHistories.Where(v => v.SessionId == sid)
                .OrderByDescending(v => v.ViewedAt).Select(v => v.ArticleId).ToListAsync();
            ids = ids.Distinct().Take(30).ToList();
            var arts = await _db.Articles.Include(a => a.Category).Where(a => ids.Contains(a.Id)).ToListAsync();
            ViewBag.Articles = ids.Select(id => arts.FirstOrDefault(a => a.Id == id)).Where(a => a != null).Cast<Article>().ToList();
        }
        else if (tab == "following")
        {
            ViewBag.FollowingUsers = await _db.UserFollows.Where(f => f.FollowerId == user.Id)
                .Join(_db.Users, f => f.FollowingId, u => u.Id, (f, u) => u)
                .OrderBy(u => u.FullName).ToListAsync();
        }
        else if (tab == "topics")
        {
            var followedCatIds = await _db.UserCategoryFollows.Where(f => f.UserId == user.Id).Select(f => f.CategoryId).ToListAsync();
            ViewBag.FollowedCatIds = followedCatIds;
            ViewBag.AllCategories = await _db.Categories.OrderBy(c => c.SortOrder).ToListAsync();
            ViewBag.Articles = followedCatIds.Any()
                ? await _db.Articles.Include(a => a.Category)
                    .Where(a => a.Status == "published" && a.CategoryId != null && followedCatIds.Contains(a.CategoryId.Value))
                    .OrderByDescending(a => a.PublishedAt).Take(20).ToListAsync()
                : new List<Article>();
        }
        else // foryou — gợi ý theo lịch sử đọc + theo dõi
        {
            var sid = HttpContext.Session.Id;
            var readCatIds = await _db.ViewHistories.Where(v => v.SessionId == sid)
                .Join(_db.Articles, v => v.ArticleId, a => a.Id, (v, a) => a.CategoryId)
                .Where(c => c != null).Select(c => c!.Value).Distinct().ToListAsync();
            var followCatIds = await _db.UserCategoryFollows.Where(f => f.UserId == user.Id).Select(f => f.CategoryId).ToListAsync();
            var catIds = readCatIds.Concat(followCatIds).Distinct().ToList();
            var followAuthorIds = await _db.UserFollows.Where(f => f.FollowerId == user.Id).Select(f => f.FollowingId).ToListAsync();

            var recs = new List<Article>();
            if (catIds.Any() || followAuthorIds.Any())
            {
                recs = await _db.Articles.Include(a => a.Category)
                    .Where(a => a.Status == "published" &&
                          ((a.CategoryId != null && catIds.Contains(a.CategoryId.Value)) ||
                           (a.AuthorUserId != null && followAuthorIds.Contains(a.AuthorUserId.Value))))
                    .OrderByDescending(a => a.PublishedAt).Take(20).ToListAsync();
            }
            if (recs.Count < 6)
            {
                var extra = await _db.Articles.Include(a => a.Category)
                    .Where(a => a.Status == "published")
                    .OrderByDescending(a => a.PublishedAt).Take(12).ToListAsync();
                foreach (var a in extra) if (recs.All(x => x.Id != a.Id)) recs.Add(a);
            }
            ViewBag.Articles = recs.Take(20).ToList();
        }
        return View();
    }

    // Lưu / bỏ lưu bài (AJAX)
    [HttpPost]
    public async Task<IActionResult> ToggleSave(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Json(new { success = false, message = "Bạn cần đăng nhập." });
        var existing = await _db.SavedArticles.FirstOrDefaultAsync(x => x.UserId == userId.Value && x.ArticleId == id);
        bool saved;
        if (existing != null) { _db.SavedArticles.Remove(existing); saved = false; }
        else { _db.SavedArticles.Add(new SavedArticle { UserId = userId.Value, ArticleId = id }); saved = true; }
        await _db.SaveChangesAsync();
        return Json(new { success = true, saved });
    }

    // Theo dõi / bỏ theo dõi chuyên mục (AJAX)
    [HttpPost]
    public async Task<IActionResult> ToggleFollowCategory(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Json(new { success = false, message = "Bạn cần đăng nhập." });
        var existing = await _db.UserCategoryFollows.FirstOrDefaultAsync(f => f.UserId == userId.Value && f.CategoryId == id);
        bool following;
        if (existing != null) { _db.UserCategoryFollows.Remove(existing); following = false; }
        else { _db.UserCategoryFollows.Add(new UserCategoryFollow { UserId = userId.Value, CategoryId = id }); following = true; }
        await _db.SaveChangesAsync();
        return Json(new { success = true, following });
    }

    // ===== NOTIFICATIONS =====
    [HttpGet]
    public async Task<IActionResult> GetUserUnreadCount()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Json(new { count = 0 });

        var count = await _db.Notifications.CountAsync(n =>
            !n.IsRead && !n.IsDeleted &&
            (
                n.TargetType == "all" ||
                (n.TargetType == "user" && n.TargetUserId == userId) ||
                (n.TargetType == "journalist" && HttpContext.Session.GetString("UserRole") == "Journalist") ||
                (n.TargetType == "email" && n.TargetEmail == HttpContext.Session.GetString("UserEmail"))
            )
        );
        return Json(new { count });
    }

    [HttpGet]
    public async Task<IActionResult> GetUserNotifications()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Json(new List<object>());

        var role = HttpContext.Session.GetString("UserRole") ?? "";
        var email = HttpContext.Session.GetString("UserEmail") ?? "";

        var list = await _db.Notifications
            .Where(n => !n.IsDeleted &&
                (
                    n.TargetType == "all" ||
                    (n.TargetType == "user" && n.TargetUserId == userId) ||
                    (n.TargetType == "journalist" && role == "Journalist") ||
                    (n.TargetType == "email" && n.TargetEmail == email)
                )
            )
            .OrderByDescending(n => n.CreatedAt)
            .Take(10)
            .Select(n => new {
                n.Id,
                n.Title,
                n.Content,
                n.Icon,
                n.IconColor,
                n.IsRead,
                n.Type,
                n.ViolationContent,
                n.ViolationReason,
                CreatedAt = n.CreatedAt.ToString("dd/MM/yyyy HH:mm")
            })
            .ToListAsync();

        return Json(list);
    }

    [HttpPost]
    public async Task<IActionResult> MarkUserNotificationRead(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Json(new { success = false });

        var n = await _db.Notifications.FindAsync(id);
        if (n == null) return Json(new { success = false });

        n.IsRead = true;
        n.ReadAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllUserNotificationsRead()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Json(new { success = false });

        var role = HttpContext.Session.GetString("UserRole") ?? "";
        var email = HttpContext.Session.GetString("UserEmail") ?? "";

        var list = await _db.Notifications.Where(n =>
            !n.IsRead && !n.IsDeleted &&
            (
                n.TargetType == "all" ||
                (n.TargetType == "user" && n.TargetUserId == userId) ||
                (n.TargetType == "journalist" && role == "Journalist") ||
                (n.TargetType == "email" && n.TargetEmail == email)
            )
        ).ToListAsync();

        foreach (var n in list) { n.IsRead = true; n.ReadAt = DateTime.Now; }
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserNotification(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Json(new { success = false });

        var n = await _db.Notifications.FindAsync(id);
        if (n == null) return Json(new { success = false });

        n.IsDeleted = true;
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserNotifications([FromBody] List<int> ids)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Json(new { success = false });

        var list = await _db.Notifications
            .Where(n => ids.Contains(n.Id))
            .ToListAsync();

        foreach (var n in list) n.IsDeleted = true;
        await _db.SaveChangesAsync();
        return Json(new { success = true, deleted = list.Count });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAllReadNotifications()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return Json(new { success = false });

        var role = HttpContext.Session.GetString("UserRole") ?? "";
        var email = HttpContext.Session.GetString("UserEmail") ?? "";

        var list = await _db.Notifications.Where(n =>
            n.IsRead && !n.IsDeleted &&
            (
                n.TargetType == "all" ||
                (n.TargetType == "user" && n.TargetUserId == userId) ||
                (n.TargetType == "journalist" && role == "Journalist") ||
                (n.TargetType == "email" && n.TargetEmail == email)
            )
        ).ToListAsync();

        foreach (var n in list) n.IsDeleted = true;
        await _db.SaveChangesAsync();
        return Json(new { success = true, deleted = list.Count });
    }

    // ===== HỘP THƯ =====
    [HttpGet]
    public async Task<IActionResult> Inbox(
        string? keyword, string? type,
        string? status, // "unread" / "read" / "" = tất cả
        DateTime? fromDate, DateTime? toDate,
        int page = 1)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login");

        var role = HttpContext.Session.GetString("UserRole") ?? "";
        var email = HttpContext.Session.GetString("UserEmail") ?? "";

        const int pageSize = 15;

        var query = _db.Notifications.Where(n =>
            !n.IsDeleted &&
            (
                n.TargetType == "all" ||
                (n.TargetType == "user" && n.TargetUserId == userId) ||
                (n.TargetType == "journalist" && role == "Journalist") ||
                (n.TargetType == "email" && n.TargetEmail == email)
            )
        ).AsQueryable();

        // Lọc theo từ khóa
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(n =>
                n.Title.Contains(keyword) ||
                n.Content.Contains(keyword));

        // Lọc theo loại
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(n => n.Type == type);

        // Lọc theo trạng thái đọc
        if (status == "unread")
            query = query.Where(n => !n.IsRead);
        else if (status == "read")
            query = query.Where(n => n.IsRead);

        // Lọc theo ngày
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
                (n.TargetType == "journalist" && role == "Journalist") ||
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
}