using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Controllers;

public class VideoController : Controller
{
    private readonly AppDbContext _db;
    public VideoController(AppDbContext db) { _db = db; }

    private static VideoItem Map(Video v) => new VideoItem
    {
        Id = v.Id, Title = v.Title, YouTubeId = v.YouTubeId,
        Source = v.Source ?? "", Views = v.Views, PublishedAt = v.PublishedAt
    };

    // Danh sách video — /video
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Video";
        ViewBag.Popular = await _db.Articles
            .Where(a => a.Status == "published")
            .OrderByDescending(a => a.Views)
            .Take(8)
            .ToListAsync();

        var dbVideos = await _db.Videos
            .Where(v => v.Status == "published")
            .OrderByDescending(v => v.PublishedAt)
            .ToListAsync();
        var items = dbVideos.Select(Map).ToList();
        if (items.Count == 0) items = VideoSampleData.Items; // chưa có video trong DB -> hiện mẫu
        return View(items);
    }

    // Xem 1 video — /Video/Watch/{id}
    public async Task<IActionResult> Watch(int id)
    {
        var v = await _db.Videos.FirstOrDefaultAsync(x => x.Id == id && x.Status == "published");
        if (v != null)
        {
            v.Views++;
            await _db.SaveChangesAsync();
            ViewData["Title"] = v.Title;
            ViewBag.Others = (await _db.Videos
                .Where(x => x.Status == "published" && x.Id != id)
                .OrderByDescending(x => x.PublishedAt).ToListAsync())
                .Select(Map).ToList();
            return View(Map(v));
        }

        // fallback dữ liệu mẫu (khi DB chưa có video)
        var sample = VideoSampleData.Items.FirstOrDefault(x => x.Id == id);
        if (sample == null) return RedirectToAction("Index");
        sample.Views++;
        ViewData["Title"] = sample.Title;
        ViewBag.Others = VideoSampleData.Items.Where(x => x.Id != id).ToList();
        return View(sample);
    }
}
