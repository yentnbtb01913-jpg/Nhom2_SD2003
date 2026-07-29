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
    // Đây là luồng xử lý hiển thị form đăng ký tài khoản
    [HttpGet]
    public IActionResult Register()
    {
        if (IsLoggedIn) return RedirectToAction("Index", "Home");
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Đây là luồng xử lý đăng ký tài khoản độc giả
    // Luồng: 1) Validate + chống trùng Username/Email (chữ thường)
    //        2) Tạo User (BCrypt hash mật khẩu, Role="Reader") -> AUTO LOGIN (nạp session)
    //        3) Gửi email xác nhận (best-effort, lỗi SMTP không chặn đăng ký)
    // Bảng: Users, EmailConfirmationTokens
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

            // Thông báo CHÀO MỪNG vào hộp thư (best-effort)
            try
            {
                var notif = HttpContext.RequestServices.GetRequiredService<WisdomITNews.Services.NotificationService>();
                await notif.SendToUserAsync(user.Id,
                    "🎉 Chào mừng đến với Wisdom IT News",
                    $"Chúc mừng {user.FullName}, bạn đã đăng ký tài khoản thành công trên Báo Wisdom IT News! "
                    + "Giờ đây bạn có thể theo dõi chuyên mục yêu thích, lưu và bình luận bài viết, nhận thông báo tin mới nhất mỗi ngày. "
                    + "Chúc bạn có những trải nghiệm đọc báo thật thú vị!",
                    type: "welcome", icon: "circle-check", iconColor: "#0e7d85");
            }
            catch (Exception nex) { _logger.LogWarning(nex, "Gửi thông báo chào mừng đăng ký thất bại"); }

            // Auto login sau khi đăng ký
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.FullName);
            HttpContext.Session.SetString("UserAvatar", user.AvatarUrl ?? "");
            HttpContext.Session.SetString("UserRole", user.Role);
            HttpContext.Session.SetString("EmailVerified", "false");

            // Gửi email xác nhận (best-effort, KHÔNG chặn đăng ký nếu gửi lỗi)
            try
            {
                var confirmSvc = HttpContext.RequestServices.GetRequiredService<WisdomITNews.Services.EmailConfirmationService>();
                var (mailOk, mailErr) = await confirmSvc.SendRegistrationAsync(user, $"{Request.Scheme}://{Request.Host}");
                if (!mailOk)
                {
                    _logger.LogWarning("Gửi email xác nhận khi đăng ký thất bại: {Err}", mailErr);
                    TempData["ConfirmError"] = "Không gửi được email xác nhận: " + (string.IsNullOrWhiteSpace(mailErr) ? "lỗi không rõ" : mailErr);
                }
            }
            catch (Exception mailEx)
            {
                _logger.LogWarning(mailEx, "Gửi email xác nhận khi đăng ký thất bại");
                TempData["ConfirmError"] = "Lỗi khi gửi email xác nhận: " + mailEx.Message;
            }

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
    // Đây là luồng xử lý hiển thị form đăng nhập
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (IsLoggedIn)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Đây là luồng xử lý đăng nhập độc giả
    // Luồng: tìm User theo username/email -> BCrypt.Verify -> chặn nếu bị khóa/xóa -> nạp session -> về returnUrl
    // Bảng: Users
    public async Task<IActionResult> Login(LoginViewModel vm, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
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
            HttpContext.Session.SetString("UserRole", user.Role);
            HttpContext.Session.SetString("EmailVerified", user.EmailVerified ? "true" : "false");

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
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
    // Đây là luồng xử lý đăng xuất độc giả (xóa session)
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
    // Đây là luồng xử lý bắt đầu đăng nhập ngoài Google/Facebook (chuyển tới nhà cung cấp)
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
    // Đây là luồng xử lý callback đăng nhập ngoài Google/Facebook
    // Luồng: lấy email/tên từ claim -> chưa có User thì tạo mới (Reader), có rồi thì đăng nhập -> nạp session
    // Bảng: Users
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

            if (!user.EmailVerified) { user.EmailVerified = true; await _db.SaveChangesAsync(); }  // đăng nhập mạng XH -> email đã xác thực
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.FullName);
            HttpContext.Session.SetString("UserAvatar", user.AvatarUrl ?? "");
            HttpContext.Session.SetString("UserRole", user.Role);
            HttpContext.Session.SetString("EmailVerified", "true");

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
    // Đây là luồng xử lý bắt đầu đăng nhập ngoài (Google) cho nhân viên/admin
    public IActionResult StaffExternalLogin(string provider = "Google")
    {
        if (string.IsNullOrWhiteSpace(provider)) provider = "Google";
        var redirectUrl = Url.Action(nameof(StaffExternalLoginCallback), "Account");
        var props = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(props, provider);
    }

    // Bước 2: Google gọi lại — khớp email với nhân viên ĐÃ được cấp (bảng Admin), set session admin.
    [HttpGet]
    // Đây là luồng xử lý callback đăng nhập ngoài (Google) cho nhân viên/admin -> nạp session admin
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
    // Đây là luồng xử lý xem trang hồ sơ công khai của một người dùng
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
    // Đây là luồng xử lý xem trang hồ sơ của chính mình
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
    // Đây là luồng xử lý cập nhật hồ sơ cá nhân (tên, email, giới thiệu)
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
    // Đây là luồng xử lý tải lên ảnh đại diện (avatar)
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
    // Đây là luồng xử lý tải lên ảnh bìa (cover)
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
    // Đây là luồng xử lý xem danh sách người theo dõi
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
    // Đây là luồng xử lý xem danh sách đang theo dõi
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
    // Đây là luồng xử lý khu cá nhân (Hub): bài theo chuyên mục đã theo dõi, bài đã lưu
    // Bảng: SavedArticles, UserCategoryFollows, Articles
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
        else // foryou — ưu tiên danh mục YÊU THÍCH (tần suất cao hơn) + lịch sử đọc + tác giả theo dõi
        {
            var sid = HttpContext.Session.Id;
            var favCatIds = await _db.UserCategoryFollows.Where(f => f.UserId == user.Id).Select(f => f.CategoryId).ToListAsync();
            var readCatIds = await _db.ViewHistories.Where(v => v.SessionId == sid)
                .Join(_db.Articles, v => v.ArticleId, a => a.Id, (v, a) => a.CategoryId)
                .Where(c => c != null).Select(c => c!.Value).Distinct().ToListAsync();
            var followAuthorIds = await _db.UserFollows.Where(f => f.FollowerId == user.Id).Select(f => f.FollowingId).ToListAsync();

            var recs = new List<Article>();

            // (1) Ưu tiên cao — bài thuộc danh mục YÊU THÍCH chiếm phần lớn feed (tần suất tăng).
            if (favCatIds.Any())
            {
                var fav = await _db.Articles.Include(a => a.Category)
                    .Where(a => a.Status == "published" && a.CategoryId != null && favCatIds.Contains(a.CategoryId.Value))
                    .OrderByDescending(a => a.PublishedAt).Take(14).ToListAsync();
                recs.AddRange(fav);
            }

            // (2) Bổ sung — danh mục đã đọc (không trùng yêu thích) + tác giả theo dõi.
            var otherCatIds = readCatIds.Where(c => !favCatIds.Contains(c)).ToList();
            if (otherCatIds.Any() || followAuthorIds.Any())
            {
                var others = await _db.Articles.Include(a => a.Category)
                    .Where(a => a.Status == "published" &&
                          ((a.CategoryId != null && otherCatIds.Contains(a.CategoryId.Value)) ||
                           (a.AuthorUserId != null && followAuthorIds.Contains(a.AuthorUserId.Value))))
                    .OrderByDescending(a => a.PublishedAt).Take(10).ToListAsync();
                foreach (var a in others) if (recs.All(x => x.Id != a.Id)) recs.Add(a);
            }

            // (3) Chưa đủ -> thêm bài mới nhất.
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
    // Đây là luồng xử lý lưu / bỏ lưu bài viết ("Báo đã lưu")
    // Bảng: SavedArticles
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
    // Đây là luồng xử lý theo dõi / bỏ theo dõi chuyên mục ("Mục của bạn")
    // Bảng: UserCategoryFollows
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
    // Đây là luồng xử lý đếm số thông báo chưa đọc của độc giả (cho badge)
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
    // Đây là luồng xử lý lấy danh sách thông báo của độc giả (dropdown)
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
    // Đây là luồng xử lý đánh dấu 1 thông báo đã đọc
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
    // Đây là luồng xử lý đánh dấu tất cả thông báo đã đọc
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
    // Đây là luồng xử lý xóa 1 thông báo của độc giả
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
    // Đây là luồng xử lý xóa nhiều thông báo cùng lúc
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
    // Đây là luồng xử lý xóa tất cả thông báo đã đọc
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
    // Đây là luồng xử lý trang hộp thư thông báo của độc giả
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

    // ===== XÁC NHẬN EMAIL =====
    [HttpGet]
    // Đây là luồng xử lý mở trang xác nhận email (từ link trong email)
    public async Task<IActionResult> ConfirmEmail(string? token, string? purpose)
    {
        ViewBag.Token = token;
        ViewBag.Purpose = purpose;
        if (string.IsNullOrWhiteSpace(token)) { ViewBag.State = "invalid"; return View(); }

        var t = await _db.EmailConfirmationTokens.FirstOrDefaultAsync(x => x.Token == token);
        if (t == null) { ViewBag.State = "invalid"; return View(); }
        ViewBag.IsSubscription = t.Purpose == EmailTokenPurpose.SubscriptionReceipt;
        ViewBag.IsTrial = t.Purpose == EmailTokenPurpose.TrialActivation;
        ViewBag.IsPurchase = t.Purpose == EmailTokenPurpose.PurchaseConfirmation;
        if (t.ConfirmedAt != null) { ViewBag.State = "already"; return View(); }
        if (t.ExpiresAt < DateTime.Now) { ViewBag.State = "expired"; return View(); }
        ViewBag.State = "pending";
        return View();
    }

    // Xác nhận THẬT (chỉ khi bấm nút) — không tự xác nhận lúc trang load.
    [HttpPost]
    [ValidateAntiForgeryToken]
    // Đây là luồng xử lý xác nhận email (đặt EmailVerified=true qua EmailConfirmationService)
    // Bảng: EmailConfirmationTokens, Users
    public async Task<IActionResult> ConfirmEmailPost(string token)
    {
        var svc = HttpContext.RequestServices.GetRequiredService<WisdomITNews.Services.EmailConfirmationService>();
        var res = await svc.ConfirmAsync(token, $"{Request.Scheme}://{Request.Host}");

        if (res.Status == WisdomITNews.Services.EmailConfirmationService.ConfirmStatus.NotFound)
        { TempData["ConfirmError"] = "Liên kết không hợp lệ."; return RedirectToAction("ConfirmEmail", new { token }); }
        if (res.Status == WisdomITNews.Services.EmailConfirmationService.ConfirmStatus.Expired)
        { TempData["ConfirmError"] = "Liên kết đã hết hạn. Vui lòng gửi lại email xác nhận."; return RedirectToAction("ConfirmEmail", new { token }); }

        if (res.Purpose == EmailTokenPurpose.Registration)
        {
            if (HttpContext.Session.GetInt32("UserId") == res.UserId)
                HttpContext.Session.SetString("EmailVerified", "true");
            TempData["ConfirmSuccess"] = res.Status == WisdomITNews.Services.EmailConfirmationService.ConfirmStatus.Confirmed
                ? "Xác nhận email thành công! Cảm ơn bạn."
                : "Tài khoản đã được xác nhận trước đó.";
            return RedirectToAction("Index", "Home");
        }

        if (res.Purpose == EmailTokenPurpose.PurchaseConfirmation)
        {
            TempData["ConfirmSuccess"] = res.Status == WisdomITNews.Services.EmailConfirmationService.ConfirmStatus.Confirmed
                ? "Thanh toán thành công! Chào mừng bạn đến với Premium."
                : "Giao dịch này đã được xác nhận trước đó.";
            if (res.TransactionId != null)
                return RedirectToAction("Result", "Subscription", new { transactionId = res.TransactionId.Value });
            return RedirectToAction("Index", "Home");
        }

        if (res.Purpose == EmailTokenPurpose.TrialActivation)
        {
            TempData["ConfirmSuccess"] = res.Status == WisdomITNews.Services.EmailConfirmationService.ConfirmStatus.Confirmed
                ? "Kích hoạt dùng thử thành công! Bạn đã có thể đọc nội dung Premium."
                : "Gói dùng thử đã được kích hoạt trước đó.";
            return RedirectToAction("Index", "Home");
        }

        // SubscriptionReceipt: chỉ ghi nhận đã xem email, KHÔNG đổi trạng thái Premium.
        TempData["ConfirmSuccess"] = "Đã ghi nhận. Cảm ơn bạn.";
        // TODO: khi trang "/Account/Subscription" (gói của tôi) được xây, redirect tới đó thay vì Home.
        return RedirectToAction("Index", "Home");
    }

    // Gửi lại email xác nhận (cooldown 60s/UserId trong service).
    [HttpPost]
    [ValidateAntiForgeryToken]
    // Đây là luồng xử lý gửi lại email xác nhận tài khoản
    public async Task<IActionResult> ResendConfirmation(string? token)
    {
        int? userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null && !string.IsNullOrWhiteSpace(token))
        {
            var t = await _db.EmailConfirmationTokens.FirstOrDefaultAsync(x => x.Token == token);
            userId = t?.UserId;
        }
        if (userId == null) { TempData["ConfirmError"] = "Không xác định được tài khoản."; return RedirectToAction("Index", "Home"); }

        var svc = HttpContext.RequestServices.GetRequiredService<WisdomITNews.Services.EmailConfirmationService>();
        var (ok, message) = await svc.ResendRegistrationAsync(userId.Value, $"{Request.Scheme}://{Request.Host}");
        TempData[ok ? "ConfirmSuccess" : "ConfirmError"] = message;
        return RedirectToAction("Index", "Home");
    }

    // [ĐÃ GỠ] Newsletter (trang đăng ký nhận tin) đã được loại bỏ.

    // ===== DANH MỤC YÊU THÍCH (gộp: chọn danh mục + bài đã lưu theo danh mục) =====
    // Đây là luồng xử lý trang chọn chuyên mục yêu thích
    public async Task<IActionResult> Favorites(int? cat)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login", new { returnUrl = "/Account/Favorites" });

        var followedCatIds = await _db.UserCategoryFollows.Where(f => f.UserId == userId.Value).Select(f => f.CategoryId).ToListAsync();
        ViewBag.FollowedCatIds = followedCatIds;
        ViewBag.AllCategories = await _db.Categories.Where(c => c.IsVisible).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync();

        var savedIds = await _db.SavedArticles.Where(s => s.UserId == userId.Value).Select(s => s.ArticleId).ToListAsync();
        var q = _db.Articles.Include(a => a.Category).Where(a => savedIds.Contains(a.Id));
        if (cat != null) q = q.Where(a => a.CategoryId == cat.Value);
        var saved = await q.OrderByDescending(a => a.PublishedAt ?? a.CreatedAt).ToListAsync();
        ViewBag.SavedArticles = saved;
        ViewBag.FilterCat = cat;

        // danh mục xuất hiện trong danh sách đã lưu (cho bộ lọc)
        var savedCatIds = await _db.Articles.Where(a => savedIds.Contains(a.Id) && a.CategoryId != null)
            .Select(a => a.CategoryId!.Value).Distinct().ToListAsync();
        ViewBag.SavedCatIds = savedCatIds;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Đây là luồng xử lý lưu danh sách chuyên mục yêu thích
    // Bảng: UserCategoryFollows
    public async Task<IActionResult> SaveFavorites(List<int>? categoryIds)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null) return RedirectToAction("Login");
        var current = await _db.UserCategoryFollows.Where(f => f.UserId == userId.Value).ToListAsync();
        _db.UserCategoryFollows.RemoveRange(current);
        if (categoryIds != null)
            foreach (var cid in categoryIds.Distinct())
                _db.UserCategoryFollows.Add(new UserCategoryFollow { UserId = userId.Value, CategoryId = cid });
        await _db.SaveChangesAsync();
        TempData["FavOk"] = "Đã lưu danh mục yêu thích.";
        return RedirectToAction("Favorites");
    }

    // ===== CÀI ĐẶT (placeholder — sẽ bổ sung sau) =====
    [HttpGet]
    // Đây là luồng xử lý hiển thị trang cài đặt tài khoản
    public IActionResult Settings()
    {
        return View();
    }
}