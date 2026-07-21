using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;
using WisdomITNews.Services;

namespace WisdomITNews.ViewComponents;

// Hiển thị quảng cáo đang hiệu lực cho vị trí (header/sidebar/in_article).
// Nhiều QC cùng vị trí -> trả CẢ danh sách, view sẽ xoay vòng bằng JS (mỗi 5s).
// Lượt hiển thị được đếm phía client qua /ad/impression/{id} mỗi lần nhảy tới 1 QC.
public class AdSlotViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    public AdSlotViewComponent(AppDbContext db) { _db = db; }

    public async Task<IViewComponentResult> InvokeAsync(string position)
    {
        // [ĐÃ GỠ Premium] Luôn hiển thị quảng cáo cho mọi người dùng.
        var now = DateTime.Now;
        // Xoay vòng theo THỨ TỰ admin đặt (DisplayOrder tăng dần), không còn random.
        var ads = await _db.Advertisements
            .Where(a => a.Position == position && a.IsActive && a.Status == "approved"
                        && (a.StartDate == null || a.StartDate <= now)
                        && (a.EndDate == null || a.EndDate >= now))
            .OrderBy(a => a.DisplayOrder).ThenBy(a => a.Id)
            .ToListAsync();

        // Chu kỳ nhảy do Admin/NhanVien cấu hình theo khu (mặc định 5s).
        var setting = await _db.AdZoneSettings.FirstOrDefaultAsync(z => z.Position == position);
        var sec = setting?.RotationSeconds ?? 5;
        if (sec < 1) sec = 1;

        return View(new AdZoneRender { Ads = ads, RotationMs = sec * 1000 });
    }
}
