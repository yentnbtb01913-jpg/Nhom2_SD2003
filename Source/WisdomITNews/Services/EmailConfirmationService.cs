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
    // Đây là luồng xử lý gửi email xác nhận khi đăng ký tài khoản
    // Luồng: tạo token (GUID, có hạn) -> lưu EmailConfirmationTokens -> gửi email chứa link xác nhận
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
    // Đây là luồng xử lý gửi lại email xác nhận đăng ký (khi token cũ hết hạn/chưa nhận được)
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
    // Đây là luồng xử lý xác nhận token email (khi người dùng bấm link trong email)
    // Luồng: 1) Tìm token còn hạn & chưa dùng
    //        2) Theo Purpose: xác nhận đăng ký (User.EmailVerified=true) / kích hoạt trial / mua gói
    //        3) Đánh dấu token.ConfirmedAt=now -> trả kết quả
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
        // [ĐÃ GỠ] Các nhánh Premium (TrialActivation/PurchaseConfirmation/SubscriptionReceipt) đã được loại bỏ.
        await _db.SaveChangesAsync();
        return new ConfirmResult { Status = ConfirmStatus.Confirmed, Purpose = t.Purpose, UserId = t.UserId, TransactionId = t.TransactionId };
    }

    // [ĐÃ GỠ] SendTrialActivationAsync / SendPurchaseConfirmationAsync / SendSubscriptionReceiptEmailAsync (Premium) đã được loại bỏ.

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
