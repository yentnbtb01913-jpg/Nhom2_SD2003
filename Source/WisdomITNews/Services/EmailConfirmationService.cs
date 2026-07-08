using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Services;

/// <summary>
/// Xác nhận email dùng chung: xác nhận đăng ký tài khoản & biên nhận gói Premium.
/// </summary>
public class EmailConfirmationService
{
    private readonly AppDbContext _db;
    private readonly EmailService _email;
    private readonly ILogger<EmailConfirmationService> _logger;
    private const int ResendCooldownSeconds = 60;
    private const int TokenLifetimeHours = 48;

    public EmailConfirmationService(AppDbContext db, EmailService email, ILogger<EmailConfirmationService> logger)
    {
        _db = db;
        _email = email;
        _logger = logger;
    }

    public enum ConfirmStatus { Confirmed, AlreadyConfirmed, Expired, NotFound }

    public class ConfirmResult
    {
        public ConfirmStatus Status { get; set; }
        public EmailTokenPurpose Purpose { get; set; }
        public int UserId { get; set; }
        public int? TransactionId { get; set; }
    }

    // Token ngẫu nhiên an toàn (32 bytes -> base64url), không đoán được.
    private static string NewToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private async Task<string> CreateTokenAsync(int userId, EmailTokenPurpose purpose, int? subscriptionId = null, int? transactionId = null)
    {
        var token = NewToken();
        _db.EmailConfirmationTokens.Add(new EmailConfirmationToken
        {
            UserId = userId,
            Token = token,
            Purpose = purpose,
            SubscriptionId = subscriptionId,
            TransactionId = transactionId,
            CreatedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddHours(TokenLifetimeHours)
        });
        await _db.SaveChangesAsync();
        return token;
    }

    // Tạo token + gửi email xác nhận đăng ký. Best-effort (không throw ra ngoài để không chặn đăng ký).
    public async Task<(bool ok, string? error)> SendRegistrationAsync(User user, string baseUrl)
    {
        try
        {
            var token = await CreateTokenAsync(user.Id, EmailTokenPurpose.Registration);
            var link = $"{baseUrl}/Account/ConfirmEmail?token={Uri.EscapeDataString(token)}";
            var html = BuildEmail(user.FullName,
                "Cảm ơn bạn đã đăng ký tài khoản tại Wisdom IT News. Vui lòng bấm nút bên dưới để xác nhận địa chỉ email của bạn.",
                "Xác nhận tài khoản", link,
                "Nếu bạn không tạo tài khoản này, vui lòng bỏ qua email.");
            var (ok, err) = await _email.SendAsync(user.Email, "Xác nhận tài khoản — Wisdom IT News", html, user.FullName);
            if (!ok) _logger.LogWarning("Gửi email xác nhận thất bại: {Err}", err);
            return (ok, err);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SendRegistrationAsync failed (userId={UserId})", user.Id);
            return (false, ex.Message);
        }
    }

    // Gửi lại email xác nhận — cooldown tối thiểu 60s theo UserId.
    public async Task<(bool ok, string message)> ResendRegistrationAsync(int userId, string baseUrl)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "Không tìm thấy tài khoản.");
        if (user.EmailVerified) return (false, "Tài khoản của bạn đã được xác nhận.");

        var last = await _db.EmailConfirmationTokens
            .Where(t => t.UserId == userId && t.Purpose == EmailTokenPurpose.Registration)
            .OrderByDescending(t => t.CreatedAt).FirstOrDefaultAsync();
        if (last != null)
        {
            var elapsed = (DateTime.Now - last.CreatedAt).TotalSeconds;
            if (elapsed < ResendCooldownSeconds)
                return (false, $"Vui lòng đợi {ResendCooldownSeconds - (int)elapsed} giây trước khi gửi lại.");
        }

        var (sent, err) = await SendRegistrationAsync(user, baseUrl);
        return sent
            ? (true, "Đã gửi lại email xác nhận. Vui lòng kiểm tra hộp thư (kể cả mục Spam).")
            : (false, "Không gửi được email: " + (string.IsNullOrWhiteSpace(err) ? "lỗi SMTP không rõ" : err));
    }

    // Xác nhận thật (khi người dùng bấm nút). Idempotent: bấm lại link cũ không gây lỗi.
    // Single-use theo nghĩa KHÔNG kích hoạt trạng thái lần 2 (đã xác nhận -> chỉ trả AlreadyConfirmed).
    public async Task<ConfirmResult> ConfirmAsync(string token, string? baseUrl = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new ConfirmResult { Status = ConfirmStatus.NotFound };

        var t = await _db.EmailConfirmationTokens.FirstOrDefaultAsync(x => x.Token == token);
        if (t == null) return new ConfirmResult { Status = ConfirmStatus.NotFound };

        if (t.ConfirmedAt != null)
            return new ConfirmResult { Status = ConfirmStatus.AlreadyConfirmed, Purpose = t.Purpose, UserId = t.UserId, TransactionId = t.TransactionId };

        if (t.ExpiresAt < DateTime.Now)
            return new ConfirmResult { Status = ConfirmStatus.Expired, Purpose = t.Purpose, UserId = t.UserId, TransactionId = t.TransactionId };

        t.ConfirmedAt = DateTime.Now;
        if (t.Purpose == EmailTokenPurpose.Registration)
        {
            var user = await _db.Users.FindAsync(t.UserId);
            if (user != null) user.EmailVerified = true;
        }
        else if (t.Purpose == EmailTokenPurpose.TrialActivation)
        {
            // Trường hợp DUY NHẤT link email được kích hoạt quyền lợi (dùng thử không liên quan tiền).
            var sub = t.SubscriptionId != null ? await _db.UserSubscriptions.FindAsync(t.SubscriptionId.Value) : null;
            if (sub != null && sub.Status == SubscriptionStatus.Trial && sub.ConfirmedAt == null)
            {
                var plan = await _db.SubscriptionPlans.FindAsync(sub.PlanId);
                int trialDays = plan?.TrialDays ?? 0;
                sub.ConfirmedAt = DateTime.Now;
                sub.StartDate = DateTime.Now;                       // tính từ lúc xác nhận thật
                sub.EndDate = DateTime.Now.AddDays(trialDays > 0 ? trialDays : 0);
                var user = await _db.Users.FindAsync(t.UserId);
                if (user != null) user.HasUsedTrial = true;
            }
        }
        else if (t.Purpose == EmailTokenPurpose.PurchaseConfirmation)
        {
            // "Webhook giả lập": chính cú bấm xác nhận email này là bước kích hoạt gói đã mua.
            var tx = t.TransactionId != null ? await _db.Transactions.FindAsync(t.TransactionId.Value) : null;
            if (tx != null && tx.Status != TransactionStatus.Success)   // idempotent
            {
                var plan = await _db.SubscriptionPlans.FindAsync(tx.PlanId);
                int durationDays = plan?.DurationDays ?? 0;
                var now = DateTime.Now;

                // Nếu đang có Active cùng plan còn hiệu lực -> cộng dồn ngày, không tạo bản ghi song song.
                var active = await _db.UserSubscriptions.FirstOrDefaultAsync(x =>
                    x.UserId == tx.UserId && x.PlanId == tx.PlanId &&
                    x.Status == SubscriptionStatus.Active && x.EndDate > now);
                UserSubscription sub;
                if (active != null)
                {
                    active.EndDate = active.EndDate.AddDays(durationDays);
                    active.ConfirmedAt ??= now;
                    sub = active;
                }
                else
                {
                    sub = new UserSubscription
                    {
                        UserId = tx.UserId,
                        PlanId = tx.PlanId,
                        Status = SubscriptionStatus.Active,
                        StartDate = now,
                        EndDate = now.AddDays(durationDays),
                        ConfirmedAt = now,
                        CreatedAt = now
                    };
                    _db.UserSubscriptions.Add(sub);
                    await _db.SaveChangesAsync();   // lấy sub.Id
                }

                tx.Status = TransactionStatus.Success;
                tx.UserSubscriptionId = sub.Id;
                tx.UpdatedAt = now;

                // Gửi email xác nhận mua thành công (best-effort, sau khi đã kích hoạt).
                try
                {
                    var user = await _db.Users.FindAsync(tx.UserId);
                    if (user != null)
                    {
                        var html = BuildEmail(user.FullName,
                            $"Thanh toán gói <strong>{System.Net.WebUtility.HtmlEncode(plan?.Name ?? "Premium")}</strong> đã thành công. Gói của bạn có hiệu lực đến {sub.EndDate:dd/MM/yyyy}.",
                            "Xem gói của tôi", (baseUrl ?? "") + "/Subscription/MySubscription",
                            "Cảm ơn bạn đã ủng hộ Wisdom IT News.");
                        await _email.SendAsync(user.Email, "Xác nhận mua gói Premium — Wisdom IT News", html, user.FullName);
                    }
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Gửi email mua thành công lỗi (txId={TxId})", tx.Id); }
            }
        }
        // SubscriptionReceipt: KHÔNG đổi trạng thái ở đây.
        await _db.SaveChangesAsync();
        return new ConfirmResult { Status = ConfirmStatus.Confirmed, Purpose = t.Purpose, UserId = t.UserId, TransactionId = t.TransactionId };
    }

    // Tạo token dùng thử + gửi email kích hoạt (nút xác nhận). Best-effort.
    public async Task<(bool ok, string? error)> SendTrialActivationAsync(User user, int subscriptionId, int trialDays, string baseUrl)
    {
        try
        {
            var token = await CreateTokenAsync(user.Id, EmailTokenPurpose.TrialActivation, subscriptionId: subscriptionId);
            var link = $"{baseUrl}/Account/ConfirmEmail?token={Uri.EscapeDataString(token)}";
            var html = BuildEmail(user.FullName,
                $"Bạn vừa yêu cầu dùng thử miễn phí {trialDays} ngày gói Premium của Wisdom IT News. Bấm nút bên dưới để kích hoạt bản dùng thử của bạn.",
                "Kích hoạt dùng thử", link,
                "Nếu bạn không yêu cầu dùng thử, vui lòng bỏ qua email này.");
            var (ok, err) = await _email.SendAsync(user.Email, "Kích hoạt dùng thử Premium — Wisdom IT News", html, user.FullName);
            if (!ok) _logger.LogWarning("Gửi email dùng thử thất bại: {Err}", err);
            return (ok, err);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SendTrialActivationAsync failed (userId={UserId})", user.Id);
            return (false, ex.Message);
        }
    }

    // Tạo token xác nhận thanh toán + gửi email nút "Xác nhận thanh toán". Best-effort.
    public async Task<(bool ok, string? error)> SendPurchaseConfirmationAsync(User user, int transactionId, string planName, decimal amount, string baseUrl)
    {
        try
        {
            var token = await CreateTokenAsync(user.Id, EmailTokenPurpose.PurchaseConfirmation, transactionId: transactionId);
            var link = $"{baseUrl}/Account/ConfirmEmail?token={Uri.EscapeDataString(token)}";
            var html = BuildEmail(user.FullName,
                $"Bạn vừa tạo yêu cầu mua gói <strong>{System.Net.WebUtility.HtmlEncode(planName)}</strong> với số tiền {amount:#,##0}đ (thanh toán mô phỏng). Bấm nút bên dưới để xác nhận và kích hoạt gói.",
                "Xác nhận thanh toán", link,
                "Nếu bạn không thực hiện giao dịch này, vui lòng bỏ qua email.");
            var (ok, err) = await _email.SendAsync(user.Email, "Xác nhận thanh toán gói Premium — Wisdom IT News", html, user.FullName);
            if (!ok) _logger.LogWarning("Gửi email xác nhận thanh toán thất bại: {Err}", err);
            return (ok, err);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SendPurchaseConfirmationAsync failed (userId={UserId})", user.Id);
            return (false, ex.Message);
        }
    }

    // ⚠️ QUAN TRỌNG: CHỈ gọi hàm này SAU KHI webhook thanh toán đã xác nhận mua gói thành công.
    // Nút xác nhận trong email loại này KHÔNG được phép kích hoạt hay thay đổi bất kỳ trạng thái Premium nào;
    // chỉ dùng để điều hướng người dùng và ghi nhận ConfirmedAt (thống kê người dùng đã xem email hay chưa).
    public async Task<bool> SendSubscriptionReceiptEmailAsync(int userId, string baseUrl)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;
        try
        {
            var token = await CreateTokenAsync(userId, EmailTokenPurpose.SubscriptionReceipt);
            var link = $"{baseUrl}/Account/ConfirmEmail?token={Uri.EscapeDataString(token)}&purpose=subscription";
            var html = BuildEmail(user.FullName,
                "Cảm ơn bạn đã đăng ký gói Premium của Wisdom IT News. Đây là biên nhận của bạn.",
                "Xem gói của tôi", link,
                "Đây là email biên nhận, không yêu cầu thao tác thanh toán thêm.");
            var (ok, _) = await _email.SendAsync(user.Email, "Biên nhận gói Premium — Wisdom IT News", html, user.FullName);
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SendSubscriptionReceiptEmailAsync failed (userId={UserId})", userId);
            return false;
        }
    }

    private static string BuildEmail(string name, string intro, string buttonText, string link, string footer)
    {
        return $@"
            <div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:24px;color:#1f2937;'>
              <h2 style='color:#e63946;margin:0 0 12px;'>Wisdom IT News</h2>
              <p>Xin chào <strong>{System.Net.WebUtility.HtmlEncode(name)}</strong>,</p>
              <p>{intro}</p>
              <p style='text-align:center;margin:28px 0;'>
                <a href='{link}' style='background:#0e7d85;color:#fff;text-decoration:none;padding:12px 28px;border-radius:6px;font-weight:700;display:inline-block;'>{buttonText}</a>
              </p>
              <p style='font-size:12px;color:#6b7280;'>Hoặc mở liên kết: <a href='{link}'>{link}</a></p>
              <hr style='border:none;border-top:1px solid #e5e7eb;margin:20px 0;'/>
              <p style='font-size:12px;color:#6b7280;'>{footer} Liên kết có hiệu lực trong 48 giờ.</p>
            </div>";
    }
}
