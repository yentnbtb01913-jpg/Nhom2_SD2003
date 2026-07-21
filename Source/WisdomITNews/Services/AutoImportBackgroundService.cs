using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Services;

// Tự động nhập bài từ RSS theo cấu hình trong bảng AutoImportSettings.
// Mỗi vòng đọc lại cấu hình MỚI NHẤT từ DB -> đổi cài đặt là áp dụng ngay, không cần sửa code.
public class AutoImportBackgroundService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<AutoImportBackgroundService> _logger;

    // Nguồn đang trong thời gian "nghỉ thử lại" sau khi lỗi: sourceId -> thời điểm được phép quét lại
    private readonly ConcurrentDictionary<int, DateTime> _retryUntil = new();

    public AutoImportBackgroundService(IServiceProvider sp, ILogger<AutoImportBackgroundService> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);

    // Đây là luồng xử lý tự động nhập RSS chạy nền
    // Luồng: 1) Chờ 15s cho app khởi động
    //        2) Mỗi vòng: đọc LẠI AutoImportSettings mới nhất từ DB (đổi cài đặt áp dụng ngay)
    //        3) Nếu Enabled -> lấy RssSources (IsActive && AutoImport) -> RunCycleAsync
    //        4) Nghỉ ScanIntervalSeconds rồi lặp
    // Bảng: AutoImportSettings, RssSources, Articles
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); } catch { }

        while (!stoppingToken.IsCancellationRequested)
        {
            int intervalSec = 60;
            try
            {
                AutoImportSettings cfg;
                List<RssSource> sources;
                using (var scope = _sp.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    cfg = await db.AutoImportSettings.AsNoTracking().FirstOrDefaultAsync(stoppingToken)
                          ?? new AutoImportSettings();   // chưa cấu hình -> dùng mặc định
                    intervalSec = Clamp(cfg.ScanIntervalSeconds, 5, 86400);

                    if (!cfg.Enabled)
                        sources = new();
                    else
                        sources = await db.RssSources
                            .Where(s => s.IsActive && s.AutoImport)
                            .AsNoTracking()
                            .ToListAsync(stoppingToken);
                }

                if (cfg.Enabled && sources.Count > 0)
                    await RunCycleAsync(cfg, sources, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AutoImport: lỗi vòng quét (bỏ qua)");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(intervalSec), stoppingToken); }
            catch { break; }
        }
    }

    // Đây là luồng xử lý một lượt quét tự động nhập RSS
    // Luồng: 1) Bỏ nguồn đang trong thời gian "nghỉ thử lại" (_retryUntil)
    //        2) SemaphoreSlim = Concurrency; mỗi nguồn gọi NewsImportService.ImportRssAsync
    //        3) Lỗi kết nối -> đặt _retryUntil = now + RetrySeconds (tạm bỏ nguồn)
    //        4) Cộng dồn số bài; dừng khi đạt MaxTotalPerRun; ghi log theo các cờ Log*
    private async Task RunCycleAsync(AutoImportSettings cfg, List<RssSource> sources, CancellationToken ct)
    {
        var now = DateTime.Now;
        int maxPerSource = Clamp(cfg.MaxPerSource, 1, 500);
        int concurrency = Clamp(cfg.Concurrency, 1, 10);
        int totalCap = Clamp(cfg.MaxTotalPerRun, 1, 100000);
        int retrySec = Clamp(cfg.RetrySeconds, 5, 86400);
        int perArticleMs = Clamp(cfg.DelayBetweenArticlesSeconds, 0, 600) * 1000;
        int betweenSourcesMs = Clamp(cfg.DelayBetweenSourcesSeconds, 0, 600) * 1000;

        // Bỏ các nguồn đang trong thời gian nghỉ thử lại
        var due = sources.Where(s =>
            !_retryUntil.TryGetValue(s.Id, out var until) || until <= now).ToList();

        int totalAdded = 0;
        var sem = new SemaphoreSlim(concurrency);
        var tasks = new List<Task>();

        foreach (var src in due)
        {
            if (Volatile.Read(ref totalAdded) >= totalCap) break;
            await sem.WaitAsync(ct);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var importer = scope.ServiceProvider.GetRequiredService<NewsImportService>();

                    var src2 = await db.RssSources.FindAsync(new object?[] { src.Id }, ct);
                    if (src2 == null) return;

                    var r = await importer.ImportRssAsync(
                        src2.FeedUrl, src2.Name, src2.DefaultCategoryId,
                        max: Math.Max(maxPerSource, 100),
                        maxNew: maxPerSource,
                        onlyNew: cfg.OnlyNew,
                        delayPerArticleMs: perArticleMs);

                    src2.LastImportAt = DateTime.Now;
                    src2.TotalImported += r.added;
                    await db.SaveChangesAsync(ct);

                    if (!r.connected)
                    {
                        _retryUntil[src.Id] = DateTime.Now.AddSeconds(retrySec);
                        if (cfg.LogConnectionError)
                            _logger.LogWarning("AutoImport: lỗi kết nối nguồn '{Name}', thử lại sau {Sec}s", src2.Name, retrySec);
                    }
                    else
                    {
                        _retryUntil.TryRemove(src.Id, out _);
                        Interlocked.Add(ref totalAdded, r.added);
                        if (r.added > 0 && cfg.LogSuccess)
                            _logger.LogInformation("AutoImport: nhập {Added} bài mới từ '{Name}'", r.added, src2.Name);
                        if (r.skipped > 0 && cfg.LogSkipDuplicate)
                            _logger.LogInformation("AutoImport: bỏ qua {Skip} bài trùng từ '{Name}'", r.skipped, src2.Name);
                    }
                }
                catch (Exception ex)
                {
                    if (cfg.LogError)
                        _logger.LogWarning(ex, "AutoImport: lỗi khi nhập nguồn #{Id}", src.Id);
                }
                finally { sem.Release(); }
            }, ct));

            // Nghỉ giữa các nguồn (chỉ áp khi chạy tuần tự để tránh dồn request)
            if (concurrency == 1 && betweenSourcesMs > 0)
            {
                try { await Task.Delay(betweenSourcesMs, ct); } catch { break; }
            }
        }

        await Task.WhenAll(tasks);
    }
}
