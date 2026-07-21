using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;
using WisdomITNews.Services;

namespace WisdomITNews.Controllers;

public class VideoController : Controller
{
    private readonly AppDbContext _db;
    private readonly AIService _ai;
    private readonly ILogger<VideoController> _logger;
    public VideoController(AppDbContext db, AIService ai, ILogger<VideoController> logger)
    {
        _db = db;
        _ai = ai;
        _logger = logger;
    }

    private static VideoItem Map(Video v) => new VideoItem
    {
        Id = v.Id, Title = v.Title, YouTubeId = v.YouTubeId,
        Source = v.Source ?? "", Views = v.Views, PublishedAt = v.PublishedAt,
        VideoType = v.VideoType ?? "youtube", VideoUrl = v.VideoUrl
    };

    // Đây là luồng xử lý hiển thị danh sách video
    // Luồng: lấy video published (mới nhất) + 8 bài xem nhiều; DB rỗng -> hiện dữ liệu mẫu VideoSampleData
    // Bảng: Videos, Articles
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

    // Đây là luồng xử lý xem 1 video
    // Luồng: 1) Tìm video published -> tăng Views++
    //        2) Nạp video khác + bình luận đã duyệt; DB không có -> fallback video mẫu
    // Bảng: Videos, VideoComments
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
            ViewBag.Comments = await _db.VideoComments
                .Where(c => c.VideoId == id && c.Status == "published")
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
            return View(Map(v));
        }

        // fallback dữ liệu mẫu (khi DB chưa có video)
        var sample = VideoSampleData.Items.FirstOrDefault(x => x.Id == id);
        if (sample == null) return RedirectToAction("Index");
        sample.Views++;
        ViewData["Title"] = sample.Title;
        ViewBag.Others = VideoSampleData.Items.Where(x => x.Id != id).ToList();
        ViewBag.Comments = new List<VideoComment>(); // video mẫu không lưu bình luận
        return View(sample);
    }

    // Đây là luồng xử lý gửi bình luận video
    // Luồng: 1) Kiểm tra nội dung + video có thật
    //        2) Lấy tên (ưu tiên user đăng nhập); ép trả lời về tối đa 1 cấp (ParentId về gốc)
    //        3) AI kiểm duyệt: score>70 -> Status="rejected"; AI lỗi vẫn cho hiện
    //        4) Lưu DB -> trả JSON
    // Bảng: VideoComments, Videos, AILogs
    // Gửi bình luận video (PHẲNG — chỉ trả lời 1 cấp). Hiện ngay sau khi AI lọc.
    [HttpPost]
    public async Task<IActionResult> PostVideoComment([FromBody] VideoCommentRequest req)
    {
        if (req == null || req.VideoId <= 0 || string.IsNullOrWhiteSpace(req.Content))
            return BadRequest(new { success = false, message = "Thiếu nội dung bình luận" });

        // chỉ cho bình luận trên video CÓ THẬT trong DB
        var exists = await _db.Videos.AnyAsync(x => x.Id == req.VideoId);
        if (!exists) return BadRequest(new { success = false, message = "Video không tồn tại" });

        // Tên: ưu tiên thành viên đăng nhập, sau đó tới tên khách nhập vào
        int? userId = null; string? sessionName = null;
        try
        {
            userId = HttpContext.Session.GetInt32("UserId");
            sessionName = HttpContext.Session.GetString("UserName");
        }
        catch { /* ignore */ }

        var name = !string.IsNullOrWhiteSpace(sessionName) ? sessionName!.Trim()
                 : (req.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) name = "Khách";

        // Ép trả lời về tối đa 1 cấp: nếu parent vốn đã là 1 trả lời thì gắn vào GỐC của nó
        int? parentId = null;
        if (req.ParentId.HasValue && req.ParentId.Value > 0)
        {
            var parent = await _db.VideoComments
                .FirstOrDefaultAsync(c => c.Id == req.ParentId.Value && c.VideoId == req.VideoId);
            if (parent != null)
                parentId = parent.ParentId ?? parent.Id;
        }

        var c = new VideoComment
        {
            VideoId = req.VideoId,
            ParentId = parentId,
            AuthorName = name,
            AuthorEmail = req.Email,
            Content = req.Content.Trim(),
            UserId = userId,
            Status = "published"
        };

        // AI lọc nội dung — hiện ngay, chỉ ẩn nếu vi phạm; AI lỗi thì vẫn cho hiện
        try
        {
            var mod = await _ai.ModerateContentAsync(c.Content);
            if (mod.Score > 70) c.Status = "rejected";
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Moderate video comment failed"); }

        _db.VideoComments.Add(c);
        await _db.SaveChangesAsync();

        if (c.Status == "rejected")
            return Ok(new { success = false, status = "rejected",
                            message = "Bình luận vi phạm quy định cộng đồng và đã bị từ chối." });

        return Ok(new
        {
            success = true,
            id = c.Id,
            parentId = c.ParentId,
            name = c.AuthorName,
            content = c.Content,
            initial = string.IsNullOrWhiteSpace(c.AuthorName) ? "?" : c.AuthorName.Substring(0, 1).ToUpper()
        });
    }
}
