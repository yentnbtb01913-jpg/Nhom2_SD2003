using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;
using WisdomITNews.Services;

namespace WisdomITNews.ViewComponents;

// Hiển thị 1 quảng cáo đang hiệu lực cho vị trí (header/sidebar/in_article).
// Nhiều QC cùng vị trí -> xoay vòng ngẫu nhiên. Tự tăng lượt hiển thị.
public class AdSlotViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    public AdSlotViewComponent(AppDbContext db) { _db = db; }

    public async Task<IViewComponentResult> InvokeAsync(string position)
    {
        // Người dùng đang có gói (Trial đã kích hoạt hoặc Premium) -> KHÔNG hiển thị quảng cáo
        var uid = HttpContext.Session.GetInt32("UserId");
        if (await PremiumAccess.HasAsync(_db, uid))
            return View((Advertisement?)null);

        var now = DateTime.Now;
        var ads = await _db.Advertisements
            .Where(a => a.Position == position && a.IsActive && a.Status == "approved"
                        && (a.StartDate == null || a.StartDate <= now)
                        && (a.EndDate == null || a.EndDate >= now))
            .ToListAsync();

        if (ads.Count == 0) return View((Advertisement?)null);

        var ad = ads[Random.Shared.Next(ads.Count)];
        try { ad.Impressions++; await _db.SaveChangesAsync(); } catch { /* đếm hiển thị best-effort */ }
        return View(ad);
    }
}
