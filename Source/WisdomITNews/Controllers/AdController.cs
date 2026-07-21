using Microsoft.AspNetCore.Mvc;
using WisdomITNews.Data;

namespace WisdomITNews.Controllers;

public class AdController : Controller
{
    private readonly AppDbContext _db;
    public AdController(AppDbContext db) { _db = db; }

    // Đây là luồng xử lý click quảng cáo
    // Luồng: 1) Tìm Advertisement theo id (không có -> về trang chủ)
    //        2) Tăng Clicks++ và lưu DB (best-effort, lỗi vẫn cho đi tiếp)
    //        3) Redirect tới TargetUrl (rỗng -> "/")
    // Bảng: Advertisements
    [Route("/ad/click/{id:int}")]
    public async Task<IActionResult> Click(int id)
    {
        var ad = await _db.Advertisements.FindAsync(id);
        if (ad == null) return Redirect("/");
        ad.Clicks++;
        try { await _db.SaveChangesAsync(); } catch { /* best-effort */ }
        var url = string.IsNullOrWhiteSpace(ad.TargetUrl) ? "/" : ad.TargetUrl;
        return Redirect(url);
    }

    // Đây là luồng xử lý đếm lượt hiển thị quảng cáo (gọi từ JS mỗi lần banner được nhảy tới)
    // Luồng: tìm Advertisement theo id -> Impressions++ -> lưu (best-effort). Trả 1x1 để không chặn UI.
    // Bảng: Advertisements
    [Route("/ad/impression/{id:int}")]
    public async Task<IActionResult> Impression(int id)
    {
        var ad = await _db.Advertisements.FindAsync(id);
        if (ad != null)
        {
            ad.Impressions++;
            try { await _db.SaveChangesAsync(); } catch { /* best-effort */ }
        }
        return NoContent();
    }
}
