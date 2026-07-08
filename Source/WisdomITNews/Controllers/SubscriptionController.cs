using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Controllers;

public class SubscriptionController : Controller
{
    private readonly AppDbContext _db;

    // Quy ước ghi Notes cho trial bị auto-hủy do hết hạn xác nhận (job GĐ6 ghi đúng chuỗi này).
    public const string AutoCancelMark = "auto-cancelled: hết hạn xác nhận";
    private const int MaxTrialRequests = 3;

    public SubscriptionController(AppDbContext db) { _db = db; }

    private int? CurrentUserId => HttpContext.Session.GetInt32("UserId");
    private string BaseUrl => $"{Request.Scheme}://{Request.Host}";

    // Đã đăng ký nhận tin chưa? (theo email user) — điều kiện trước khi vào Premium.
    private async Task<bool> IsNewsletterSubscribedAsync(int userId)
    {
        var email = (await _db.Users.FindAsync(userId))?.Email;
        if (string.IsNullOrWhiteSpace(email)) return false;
        return await _db.NewsletterSubscribers.AnyAsync(x => x.Email == email && x.Status == "active");
    }

    // Chưa đăng ký nhận tin -> chuyển về trang đăng ký + thông báo. Trả true nếu đã chuyển hướng.
    private async Task<IActionResult?> RequireNewsletterAsync()
    {
        if (CurrentUserId == null) return null;
        if (await IsNewsletterSubscribedAsync(CurrentUserId.Value)) return null;
        TempData["NewsletterNotice"] = "Vui lòng đăng ký nhận tin trước khi đăng ký gói Premium.";
        return RedirectToAction("Newsletter", "Account");
    }

    private Task<SubscriptionPlan?> ActivePlanAsync() =>
        _db.SubscriptionPlans.Include(p => p.Features)
           .Where(p => p.IsActive).OrderBy(p => p.Id).FirstOrDefaultAsync();

    // ===== BẢNG GIÁ =====
    [HttpGet]
    public async Task<IActionResult> Pricing()
    {
        var gate = await RequireNewsletterAsync();
        if (gate != null) return gate;
        var plan = await ActivePlanAsync();
        ViewBag.Plan = plan;

        bool loggedIn = CurrentUserId != null;
        bool showTrial = false, hasActive = false;
        if (loggedIn)
        {
            var user = await _db.Users.FindAsync(CurrentUserId.Value);
            showTrial = user != null && !user.HasUsedTrial && plan != null && plan.TrialDays > 0;
            hasActive = await _db.UserSubscriptions.AnyAsync(s =>
                s.UserId == CurrentUserId.Value &&
                (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial) &&
                s.ConfirmedAt != null && s.EndDate > DateTime.Now);
        }
        ViewBag.LoggedIn = loggedIn;
        ViewBag.ShowTrial = showTrial;
        ViewBag.HasActivePremium = hasActive;
        return View();
    }

    // ===== DÙNG THỬ (kích hoạt qua email) =====
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartTrial()
    {
        if (CurrentUserId == null)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Pricing", "Subscription") });

        var user = await _db.Users.FindAsync(CurrentUserId.Value);
        if (user == null)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Pricing", "Subscription") });

        var gate = await RequireNewsletterAsync();
        if (gate != null) return gate;

        var plan = await ActivePlanAsync();
        if (plan == null || plan.TrialDays <= 0)
        { TempData["SubError"] = "Hiện chưa có gói dùng thử."; return RedirectToAction("Pricing"); }

        if (user.HasUsedTrial)
        { TempData["SubError"] = "Bạn đã sử dụng lượt dùng thử rồi."; return RedirectToAction("Pricing"); }

        var svc = HttpContext.RequestServices.GetRequiredService<WisdomITNews.Services.EmailConfirmationService>();

        // Nếu đang có trial chờ xác nhận & token còn hạn -> gửi lại email, không tạo bản ghi mới.
        var pending = await _db.UserSubscriptions
            .Where(s => s.UserId == user.Id && s.Status == SubscriptionStatus.Trial && s.ConfirmedAt == null)
            .OrderByDescending(s => s.Id).FirstOrDefaultAsync();
        if (pending != null)
        {
            var tok = await _db.EmailConfirmationTokens
                .Where(t => t.Purpose == EmailTokenPurpose.TrialActivation && t.SubscriptionId == pending.Id && t.ConfirmedAt == null)
                .OrderByDescending(t => t.CreatedAt).FirstOrDefaultAsync();
            if (tok != null && tok.ExpiresAt > DateTime.Now)
            {
                await svc.SendTrialActivationAsync(user, pending.Id, plan.TrialDays, BaseUrl);
                TempData["SubSuccess"] = "Đã gửi lại email kích hoạt dùng thử. Vui lòng kiểm tra hộp thư (kể cả mục Spam).";
                return RedirectToAction("Pricing");
            }
        }

        // Giới hạn 3 lần: đếm trial đã bị auto-hủy do hết hạn xác nhận.
        int cancelledCount = await _db.UserSubscriptions.CountAsync(s =>
            s.UserId == user.Id && s.Status == SubscriptionStatus.Cancelled &&
            s.ConfirmedAt == null && s.Notes == AutoCancelMark);
        if (cancelledCount >= MaxTrialRequests)
        { TempData["SubError"] = "Bạn đã yêu cầu dùng thử quá số lần cho phép. Vui lòng nâng cấp để tiếp tục."; return RedirectToAction("Pricing"); }

        var sub = new UserSubscription
        {
            UserId = user.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Trial,
            StartDate = DateTime.Now,
            EndDate = DateTime.Now.AddDays(plan.TrialDays),   // tạm; sẽ tính lại khi xác nhận
            ConfirmedAt = null,
            CreatedAt = DateTime.Now
        };
        _db.UserSubscriptions.Add(sub);
        await _db.SaveChangesAsync();

        var (ok, err) = await svc.SendTrialActivationAsync(user, sub.Id, plan.TrialDays, BaseUrl);
        TempData[ok ? "SubSuccess" : "SubError"] = ok
            ? $"Đã gửi email kích hoạt dùng thử {plan.TrialDays} ngày tới {user.Email}. Vui lòng mở email và bấm \"Kích hoạt dùng thử\"."
            : "Không gửi được email kích hoạt: " + (string.IsNullOrWhiteSpace(err) ? "lỗi SMTP" : err);
        return RedirectToAction("Pricing");
    }

    // ===== CHECKOUT =====
    [HttpGet]
    public async Task<IActionResult> Checkout(int planId)
    {
        if (CurrentUserId == null)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Checkout", "Subscription", new { planId }) });
        var gate = await RequireNewsletterAsync();
        if (gate != null) return gate;
        var plan = await _db.SubscriptionPlans.Include(p => p.Features).FirstOrDefaultAsync(p => p.Id == planId && p.IsActive);
        if (plan == null) { TempData["SubError"] = "Gói không tồn tại hoặc đã ngừng bán."; return RedirectToAction("Pricing"); }
        ViewBag.Plan = plan;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(int planId, string method, string? coupon)
    {
        if (CurrentUserId == null)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Checkout", "Subscription", new { planId }) });
        var user = await _db.Users.FindAsync(CurrentUserId.Value);
        if (user == null) return RedirectToAction("Login", "Account");
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId && p.IsActive);
        if (plan == null) { TempData["SubError"] = "Gói không tồn tại."; return RedirectToAction("Pricing"); }

        // Ô mã giảm giá: chỉ nhận UI trong lần này, chưa xử lý logic (TODO hệ thống coupon).
        string label = method == "momo" ? "Momo (giả lập)" : "VNPay (giả lập)";
        var tx = new Transaction
        {
            UserId = user.Id,
            PlanId = plan.Id,
            Amount = plan.Price,
            PaymentMethodLabel = label,
            Status = TransactionStatus.Pending,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync();

        var svc = HttpContext.RequestServices.GetRequiredService<WisdomITNews.Services.EmailConfirmationService>();
        await svc.SendPurchaseConfirmationAsync(user, tx.Id, plan.Name, plan.Price, BaseUrl);
        return RedirectToAction("Processing", new { transactionId = tx.Id });
    }

    // ===== ĐANG XỬ LÝ (poll) =====
    [HttpGet]
    public async Task<IActionResult> Processing(int transactionId)
    {
        if (CurrentUserId == null) return RedirectToAction("Login", "Account");
        var tx = await _db.Transactions.FindAsync(transactionId);
        if (tx == null || tx.UserId != CurrentUserId.Value) return RedirectToAction("Pricing");
        ViewBag.TransactionId = transactionId;
        return View();
    }

    // JSON cho AJAX poll — chỉ trả trạng thái từ DB.
    [HttpGet]
    public async Task<IActionResult> PaymentStatus(int transactionId)
    {
        var tx = await _db.Transactions.FindAsync(transactionId);
        if (tx == null || CurrentUserId == null || tx.UserId != CurrentUserId.Value)
            return Json(new { status = "NotFound" });
        return Json(new { status = tx.Status.ToString() });
    }

    // ===== KẾT QUẢ (chỉ đọc DB, KHÔNG tin query string) =====
    [HttpGet]
    public async Task<IActionResult> Result(int transactionId)
    {
        if (CurrentUserId == null) return RedirectToAction("Login", "Account");
        var tx = await _db.Transactions.Include(t => t.Plan).FirstOrDefaultAsync(t => t.Id == transactionId);
        if (tx == null || tx.UserId != CurrentUserId.Value) return RedirectToAction("Pricing");
        ViewBag.Tx = tx;
        return View();
    }

    // Gửi lại email xác nhận thanh toán (cooldown 60s).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendPurchase(int transactionId)
    {
        if (CurrentUserId == null) return RedirectToAction("Login", "Account");
        var tx = await _db.Transactions.FindAsync(transactionId);
        if (tx == null || tx.UserId != CurrentUserId.Value)
        { TempData["SubError"] = "Giao dịch không hợp lệ."; return RedirectToAction("Pricing"); }
        if (tx.Status == TransactionStatus.Success)
        { TempData["SubSuccess"] = "Giao dịch đã hoàn tất."; return RedirectToAction("Result", new { transactionId }); }

        var last = await _db.EmailConfirmationTokens
            .Where(t => t.Purpose == EmailTokenPurpose.PurchaseConfirmation && t.TransactionId == transactionId)
            .OrderByDescending(t => t.CreatedAt).FirstOrDefaultAsync();
        if (last != null)
        {
            var el = (DateTime.Now - last.CreatedAt).TotalSeconds;
            if (el < 60) { TempData["SubError"] = $"Vui lòng đợi {60 - (int)el} giây trước khi gửi lại."; return RedirectToAction("Processing", new { transactionId }); }
        }
        var user = await _db.Users.FindAsync(CurrentUserId.Value);
        var plan = await _db.SubscriptionPlans.FindAsync(tx.PlanId);
        var svc = HttpContext.RequestServices.GetRequiredService<WisdomITNews.Services.EmailConfirmationService>();
        await svc.SendPurchaseConfirmationAsync(user!, tx.Id, plan?.Name ?? "Premium", tx.Amount, BaseUrl);
        TempData["SubSuccess"] = "Đã gửi lại email xác nhận thanh toán. Vui lòng kiểm tra hộp thư.";
        return RedirectToAction("Processing", new { transactionId });
    }

    // ===== GÓI CỦA TÔI =====
    [HttpGet]
    public async Task<IActionResult> MySubscription()
    {
        if (CurrentUserId == null)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("MySubscription", "Subscription") });
        var now = DateTime.Now;
        var subs = await _db.UserSubscriptions.Include(s => s.Plan)
            .Where(s => s.UserId == CurrentUserId.Value)
            .OrderByDescending(s => s.Id).ToListAsync();
        ViewBag.Subs = subs;
        ViewBag.Current = subs.FirstOrDefault(s =>
            (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial) &&
            s.ConfirmedAt != null && s.EndDate > now);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSubscription(int id)
    {
        if (CurrentUserId == null) return RedirectToAction("Login", "Account");
        var sub = await _db.UserSubscriptions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == CurrentUserId.Value);
        if (sub == null) { TempData["SubError"] = "Không tìm thấy gói."; return RedirectToAction("MySubscription"); }
        if (sub.Status == SubscriptionStatus.Active || sub.Status == SubscriptionStatus.Trial)
        {
            sub.Status = SubscriptionStatus.Cancelled;
            sub.Notes = (string.IsNullOrEmpty(sub.Notes) ? "" : sub.Notes + " | ") + "Người dùng tự hủy " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            await _db.SaveChangesAsync();
            TempData["SubSuccess"] = "Đã hủy gói của bạn.";
        }
        return RedirectToAction("MySubscription");
    }

    // ===== LỊCH SỬ GIAO DỊCH =====
    [HttpGet]
    public async Task<IActionResult> TransactionHistory()
    {
        if (CurrentUserId == null)
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("TransactionHistory", "Subscription") });
        var list = await _db.Transactions.Include(t => t.Plan)
            .Where(t => t.UserId == CurrentUserId.Value)
            .OrderByDescending(t => t.Id).ToListAsync();
        ViewBag.List = list;
        return View();
    }
}
