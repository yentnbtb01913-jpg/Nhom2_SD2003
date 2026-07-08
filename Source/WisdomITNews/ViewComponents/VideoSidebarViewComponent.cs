using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;

namespace WisdomITNews.ViewComponents;

// Cột Video ở sidebar phải: danh sách video mới nhất, có tìm kiếm + cuộn.
public class VideoSidebarViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    public VideoSidebarViewComponent(AppDbContext db) { _db = db; }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var vids = await _db.Videos.OrderByDescending(v => v.PublishedAt).Take(40).ToListAsync();
        return View(vids);
    }
}
