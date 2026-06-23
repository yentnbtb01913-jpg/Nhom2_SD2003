using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Controllers;

public class VideoController : Controller
{
    private readonly AppDbContext _db;
    public VideoController(AppDbContext db) { _db = db; }

    // Danh sách video — /video
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Video";
        ViewBag.Popular = await _db.Articles
            .Where(a => a.Status == "published")
            .OrderByDescending(a => a.Views)
            .Take(8)
            .ToListAsync();
        return View(VideoSampleData.Items);
    }

    // Xem 1 video — /Video/Watch/{id}
    public IActionResult Watch(int id)
    {
        var video = VideoSampleData.Items.FirstOrDefault(v => v.Id == id);
        if (video == null) return RedirectToAction("Index");
        video.Views++; // mẫu: tăng lượt xem trong bộ nhớ
        ViewData["Title"] = video.Title;
        ViewBag.Others = VideoSampleData.Items.Where(v => v.Id != id).ToList();
        return View(video);
    }
}
