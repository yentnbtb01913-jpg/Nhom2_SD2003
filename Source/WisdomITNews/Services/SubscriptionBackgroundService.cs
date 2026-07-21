using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;

namespace WisdomITNews.Services;

/// <summary>
/// Job nền: tự động TẮT quảng cáo hết hạn (EndDate &lt; now -> IsActive=false).
/// (Đã gỡ toàn bộ phần Premium; giữ tên lớp để không phải sửa đăng ký ở Program.cs.)
/// </summary>
public class SubscriptionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SubscriptionBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    public SubscriptionBackgroundService(IServiceScopeFactory scopeFactory, ILogger<SubscriptionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch { }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.Now;
                var expiredAds = await db.Advertisements
                    .Where(a => a.IsActive && a.EndDate != null && a.EndDate < now)
                    .ToListAsync(stoppingToken);
                foreach (var a in expiredAds) a.IsActive = false;
                if (expiredAds.Count > 0) await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Job tắt QC hết hạn lỗi"); }
            try { await Task.Delay(Interval, stoppingToken); } catch { }
        }
    }
}
