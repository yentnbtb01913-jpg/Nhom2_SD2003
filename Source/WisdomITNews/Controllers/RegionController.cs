using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Controllers;

/// <summary>
/// [J] Bản đồ tin tức theo khu vực — render danh sách bài + map Leaflet.
/// </summary>
public class RegionController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<RegionController> _logger;

    // Danh sách các vùng hỗ trợ
    private static readonly Dictionary<string, (string Name, string WeatherCity, double Lat, double Lng)> Regions = new()
    {
        ["dong-nai"] = ("Đồng Nai", "Bien Hoa", 10.9574, 106.8426),
        ["ha-noi"] = ("Hà Nội", "Hanoi", 21.0285, 105.8542),
        ["ho-chi-minh"] = ("TP. Hồ Chí Minh", "Ho Chi Minh", 10.8231, 106.6297),
        ["da-nang"] = ("Đà Nẵng", "Da Nang", 16.0544, 108.2022),
        ["hai-phong"] = ("Hải Phòng", "Hai Phong", 20.8449, 106.6881),
        ["can-tho"] = ("Cần Thơ", "Can Tho", 10.0452, 105.7469),
    };

    public RegionController(AppDbContext db, ILogger<RegionController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// API chuyển vùng — lưu vào session và redirect
    /// </summary>
    [HttpPost]
    [Route("/chuyen-vung")]
    public IActionResult SetRegion([FromForm] string region)
    {
        if (Regions.ContainsKey(region))
        {
            HttpContext.Session.SetString("CurrentRegion", region);
        }
        // Redirect đến trang vùng tương ứng
        return Redirect($"/{region}");
    }

    /// <summary>
    /// API lấy thông tin vùng hiện tại (cho JavaScript gọi)
    /// </summary>
    [HttpGet]
    [Route("/api/region/current")]
    public IActionResult GetCurrentRegion()
    {
        var current = HttpContext.Session.GetString("CurrentRegion") ?? "dong-nai";
        if (Regions.TryGetValue(current, out var info))
        {
            return Json(new { slug = current, name = info.Name, weatherCity = info.WeatherCity });
        }
        return Json(new { slug = "dong-nai", name = "Đồng Nai", weatherCity = "Bien Hoa" });
    }

    /// <summary>
    /// API lấy danh sách tất cả các vùng
    /// </summary>
    [HttpGet]
    [Route("/api/regions")]
    public IActionResult GetAllRegions()
    {
        var list = Regions.Select(r => new { slug = r.Key, name = r.Value.Name }).ToList();
        return Json(list);
    }

    [Route("/dong-nai")]
    public Task<IActionResult> DongNai()
    {
        HttpContext.Session.SetString("CurrentRegion", "dong-nai");
        return RegionAsync("dong-nai");
    }

    [Route("/ha-noi")]
    public Task<IActionResult> HaNoi()
    {
        HttpContext.Session.SetString("CurrentRegion", "ha-noi");
        return RegionAsync("ha-noi");
    }

    [Route("/ho-chi-minh")]
    public Task<IActionResult> HoChiMinh()
    {
        HttpContext.Session.SetString("CurrentRegion", "ho-chi-minh");
        return RegionAsync("ho-chi-minh");
    }

    [Route("/da-nang")]
    public Task<IActionResult> DaNang()
    {
        HttpContext.Session.SetString("CurrentRegion", "da-nang");
        return RegionAsync("da-nang");
    }

    [Route("/hai-phong")]
    public Task<IActionResult> HaiPhong()
    {
        HttpContext.Session.SetString("CurrentRegion", "hai-phong");
        return RegionAsync("hai-phong");
    }

    [Route("/can-tho")]
    public Task<IActionResult> CanTho()
    {
        HttpContext.Session.SetString("CurrentRegion", "can-tho");
        return RegionAsync("can-tho");
    }

    private async Task<IActionResult> RegionAsync(string regionSlug)
    {
        if (!Regions.TryGetValue(regionSlug, out var regionInfo))
            return NotFound();

        ViewData["Title"] = $"Tin tức {regionInfo.Name}";
        ViewData["ActiveNav"] = regionSlug;

        try
        {
            // Lấy tất cả bài published của vùng
            var allArticles = await _db.Articles
                .Include(a => a.Category)
                .Where(a => a.Status == "published" && a.Region == regionSlug)
                .OrderByDescending(a => a.PublishedAt)
                .Take(50)
                .ToListAsync();

            // Bài nổi bật: ưu tiên IsFeatured, nếu thiếu thì lấy bài mới nhất có ảnh
            var featured = allArticles
                .Where(a => a.IsFeatured && !string.IsNullOrEmpty(a.Thumbnail))
                .Take(3)
                .ToList();

            if (featured.Count < 3)
            {
                var existIds = featured.Select(a => a.Id).ToHashSet();
                var more = allArticles
                    .Where(a => !existIds.Contains(a.Id) && !string.IsNullOrEmpty(a.Thumbnail))
                    .Take(3 - featured.Count)
                    .ToList();
                featured.AddRange(more);
            }

            // Danh sách bài còn lại (không trùng featured)
            var featuredIds = featured.Select(a => a.Id).ToHashSet();
            var remaining = allArticles
                .Where(a => !featuredIds.Contains(a.Id))
                .ToList();

            var vm = new RegionViewModel
            {
                RegionSlug = regionSlug,
                RegionName = regionInfo.Name,
                CenterLat = regionInfo.Lat,
                CenterLng = regionInfo.Lng,
                FeaturedArticles = featured,
                Articles = remaining
            };
            return View("Region", vm);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Region {Slug} failed", regionSlug);
            return View("Region", new RegionViewModel
            {
                RegionSlug = regionSlug,
                RegionName = regionInfo.Name,
                CenterLat = regionInfo.Lat,
                CenterLng = regionInfo.Lng
            });
        }
    }
}
