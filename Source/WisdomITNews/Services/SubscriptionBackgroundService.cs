using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Services;

/// <summary>
/// Job nền cho Gói Premium. Chạy định kỳ, các tác vụ đều idempotent/có cờ chống lặp
/// nên chạy lại nhiều lần vô hại:
///  - Transaction Pending quá 48h  -> Failed
///  - Trial chưa xác nhận quá 48h   -> Cancelled (Notes = AutoCancelMark)
///  - Gói hết hạn (EndDate &lt; now) -> Expired
///  - Nhắc hết hạn trước 3 ngày (gửi 1 lần, đánh dấu ExpiryReminderSentAt)
/// </summary>
public class SubscriptionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SubscriptionBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    // PHẢI khớp đúng chuỗi trong SubscriptionController.AutoCancelMark (đếm giới hạn 3 lần trial).
    public const string AutoCancelMark = "auto-cancelled: hết hạn xác nhận";

    public SubscriptionBackgroundService(IServiceScopeFactory scopeFactory, ILogger<SubscriptionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Chờ app khởi động + migrate xong.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch { }
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogWarning(ex, "SubscriptionBackgroundService tick lỗi"); }
            try { await Task.Delay(Interval, stoppingToken); } catch { }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<EmailService>();
        var now = DateTime.Now;

        // 1) Transaction Pending quá 48h -> Failed
        var pendingCutoff = now.AddHours(-48);
        var stalePending = await db.Transactions
            .Where(t => t.Status == TransactionStatus.Pending && t.CreatedAt < pendingCutoff)
            .ToListAsync(ct);
        foreach (var t in stalePending) { t.Status = TransactionStatus.Failed; t.UpdatedAt = now; }

        // 2) Trial chưa xác nhận quá 48h -> Cancelled
        var trialCutoff = now.AddHours(-48);
        var staleTrials = await db.UserSubscriptions
            .Where(s => s.Status == SubscriptionStatus.Trial && s.ConfirmedAt == null && s.CreatedAt < trialCutoff)
            .ToListAsync(ct);
        foreach (var s in staleTrials) { s.Status = SubscriptionStatus.Cancelled; s.Notes = AutoCancelMark; }

        // 3) Gói hết hạn -> Expired
        var expired = await db.UserSubscriptions
            .Where(s => (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial)
                        && s.ConfirmedAt != null && s.EndDate < now)
            .ToListAsync(ct);
        foreach (var s in expired) s.Status = SubscriptionStatus.Expired;

        if (stalePending.Count > 0 || staleTrials.Count > 0 || expired.Count > 0)
            await db.SaveChangesAsync(ct);

        // 3b) Quảng cáo hết hạn -> tự tắt (đưa vào danh sách "cần gia hạn")
        var expiredAds = await db.Advertisements
            .Where(a => a.IsActive && a.EndDate != null && a.EndDate < now)
            .ToListAsync(ct);
        foreach (var a in expiredAds) a.IsActive = false;
        if (expiredAds.Count > 0) await db.SaveChangesAsync(ct);

        // 4) Nhắc hết hạn trước 3 ngày (chỉ Active, gửi 1 lần)
        var soon = now.AddDays(3);
        var toRemind = await db.UserSubscriptions.Include(s => s.Plan)
            .Where(s => s.Status == SubscriptionStatus.Active && s.ConfirmedAt != null
                        && s.ExpiryReminderSentAt == null
                        && s.EndDate > now && s.EndDate <= soon)
            .ToListAsync(ct);
        foreach (var s in toRemind)
        {
            var user = await db.Users.FindAsync(new object?[] { s.UserId }, ct);
            if (user != null)
            {
                try
                {
                    var html = BuildReminderHtml(user.FullName, s.Plan?.Name ?? "Premium", s.EndDate);
                    await email.SendAsync(user.Email, "Gói Premium sắp hết hạn — Wisdom IT News", html, user.FullName);
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Gửi email nhắc hết hạn lỗi (subId={Id})", s.Id); }
            }
            s.ExpiryReminderSentAt = now;   // đánh dấu đã xử lý (dù gửi lỗi) để không lặp
        }
        if (toRemind.Count > 0) await db.SaveChangesAsync(ct);
    }

    private static string BuildReminderHtml(string name, string planName, DateTime endDate)
    {
        var n = System.Net.WebUtility.HtmlEncode(name);
        var p = System.Net.WebUtility.HtmlEncode(planName);
        return $@"
            <div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;padding:24px;color:#1f2937;'>
              <h2 style='color:#e63946;margin:0 0 12px;'>Wisdom IT News</h2>
              <p>Xin chào <strong>{n}</strong>,</p>
              <p>Gói <strong>{p}</strong> của bạn sẽ hết hạn vào <strong>{endDate:dd/MM/yyyy}</strong> (còn dưới 3 ngày).</p>
              <p>Để tiếp tục đọc nội dung Premium, vui lòng gia hạn gói của bạn.</p>
              <p style='text-align:center;margin:24px 0;'>
                <a href='#' style='background:#0e7d85;color:#fff;text-decoration:none;padding:12px 26px;border-radius:6px;font-weight:700;display:inline-block;'>Gia hạn ngay</a>
              </p>
              <hr style='border:none;border-top:1px solid #e5e7eb;margin:18px 0;'/>
              <p style='font-size:12px;color:#6b7280;'>Cảm ơn bạn đã ủng hộ Wisdom IT News.</p>
            </div>";
    }
}
