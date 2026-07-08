using Microsoft.AspNetCore.Mvc;
using WisdomITNews.Data;

namespace WisdomITNews.Controllers;

public class AdController : Controller
{
    private readonly AppDbContext _db;
    public AdController(AppDbContext db) { _db = db; }

    // Theo dõi click rồi chuyển tới link đích
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
}
