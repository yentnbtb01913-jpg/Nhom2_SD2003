using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;

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
        if (string.IsNullOrWhiteSpace(vm.Username) ||
            string.IsNullOrWhiteSpace(vm.Email) ||
            string.IsNullOrWhiteSpace(vm.Password) ||
            string.IsNullOrWhiteSpace(vm.FullName))
        {
            vm.Error = "Vui lòng điền đầy đủ thông tin";
            return View(vm);
        }

        if (vm.Password.Length < 6)
        {
            vm.Error = "Mật khẩu tối thiểu 6 ký tự";
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
        if (string.IsNullOrWhiteSpace(vm.UsernameOrEmail) || string.IsNullOrWhiteSpace(vm.Password))
        {
            vm.Error = "Vui lòng nhập đầy đủ";
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

    
}