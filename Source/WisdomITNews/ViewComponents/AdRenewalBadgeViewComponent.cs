using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;

namespace WisdomITNews.ViewComponents;

// Badge đếm tin nhắn gia hạn QC chưa đọc. side = "journalist" | "admin".
public class AdRenewalBadgeViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    public AdRenewalBadgeViewComponent(AppDbContext db) { _db = db; }

    public async Task<IViewComponentResult> InvokeAsync(string side)
    {
        int count = 0;
        try
        {
            if (side == "journalist")
            {
                var jid = HttpContext.Session.GetInt32("JournalistId");
                if (jid != null)
                    count = await _db.AdRenewalMessages.CountAsync(m =>
                        m.SenderRole != "journalist" && !m.IsReadByJournalist &&
                        _db.Advertisements.Any(a => a.Id == m.AdvertisementId && a.CreatedByUserId == jid));
            }
            else // admin / nhân viên
            {
                if (int.TryParse(HttpContext.Session.GetString("AdminId"), out var aid) && aid > 0)
                    count = await _db.AdRenewalMessages.CountAsync(m =>
                        m.SenderRole == "journalist" && !m.IsReadByAdmin);
            }
        }
        catch { count = 0; }   // bảng chưa migrate -> không vỡ layout
        return View(count);
    }
}
