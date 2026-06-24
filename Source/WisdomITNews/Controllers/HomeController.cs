using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<HomeController> _logger;
    public HomeController(AppDbContext db, ILogger<HomeController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        // Đọc vùng hiện tại từ session
        var currentRegion = HttpContext.Session.GetString("CurrentRegion") ?? "dong-nai";

        // Query cơ sở: bài published + cùng vùng HOẶC bài không có vùng (Region = null)
        var regionQuery = _db.Articles
            .Include(a => a.Category)
            .Where(a => a.Status == "published" && (a.Region == currentRegion || a.Region == null || a.Region == ""));

        // Query tất cả (fallback nếu vùng ít bài)
        var allQuery = _db.Articles
            .Include(a => a.Category)
            .Where(a => a.Status == "published");

        // Featured: ưu tiên bài vùng đó, nếu thiếu thì lấy thêm bài chung
        var featuredRegion = await regionQuery
            .Where(a => a.IsFeatured)
            .OrderByDescending(a => a.PublishedAt)
            .Take(3).ToListAsync();

        if (featuredRegion.Count < 3)
        {
            var existIds = featuredRegion.Select(a => a.Id).ToList();
            var more = await allQuery
                .Where(a => a.IsFeatured && !existIds.Contains(a.Id))
                .OrderByDescending(a => a.PublishedAt)
                .Take(3 - featuredRegion.Count).ToListAsync();
            featuredRegion.AddRange(more);
        }

        // Latest: ưu tiên vùng, fallback chung
        var latestRegion = await regionQuery
            .OrderByDescending(a => a.PublishedAt)
            .Take(12).ToListAsync();

        if (latestRegion.Count < 12)
        {
            var existIds = latestRegion.Select(a => a.Id).ToList();
            var more = await allQuery
                .Where(a => !existIds.Contains(a.Id))
                .OrderByDescending(a => a.PublishedAt)
                .Take(12 - latestRegion.Count).ToListAsync();
            latestRegion.AddRange(more);
        }

        // AI articles: ưu tiên vùng
        var aiRegion = await regionQuery
            .Where(a => a.CategoryId == 3)
            .OrderByDescending(a => a.PublishedAt)
            .Take(3).ToListAsync();

        if (aiRegion.Count < 3)
        {
            var existIds = aiRegion.Select(a => a.Id).ToList();
            var more = await allQuery
                .Where(a => a.CategoryId == 3 && !existIds.Contains(a.Id))
                .OrderByDescending(a => a.PublishedAt)
                .Take(3 - aiRegion.Count).ToListAsync();
            aiRegion.AddRange(more);
        }

        // Nóng 24H: xếp theo lượt ĐỌC THẬT trong 24 giờ (ViewHistory); thiếu thì bù theo tổng lượt xem
        var hotSince = DateTime.Now.AddHours(-24);
        var hotIds = await _db.ViewHistories
            .Where(v => v.ViewedAt >= hotSince)
            .GroupBy(v => v.ArticleId)
            .Select(g => new { Id = g.Key, C = g.Count() })
            .OrderByDescending(x => x.C)
            .Take(5).Select(x => x.Id).ToListAsync();
        var hotArts = await allQuery.Where(a => hotIds.Contains(a.Id)).ToListAsync();
        var popularRegion = hotIds
            .Select(id => hotArts.FirstOrDefault(a => a.Id == id))
            .Where(a => a != null).Cast<Article>().ToList();

        if (popularRegion.Count < 5)
        {
            var existIds = popularRegion.Select(a => a.Id).ToList();
            var more = await allQuery
                .Where(a => !existIds.Contains(a.Id))
                .OrderByDescending(a => a.Views)
                .Take(5 - popularRegion.Count).ToListAsync();
            popularRegion.AddRange(more);
        }

        var vm = new HomeViewModel
        {
            FeaturedArticles = featuredRegion,
            LatestArticles = latestRegion,
            AIArticles = aiRegion,
            PopularArticles = popularRegion,

            Categories = await _db.Categories
                .Where(c => c.IsVisible)
                .OrderBy(c => c.SortOrder)
                .ToListAsync(),

            Tags = await _db.Tags.Take(12).ToListAsync()
        };

        // Truyền tên vùng hiện tại cho View (nếu muốn hiển thị)
        ViewBag.CurrentRegion = currentRegion;

        return View(vm);
    }

    public async Task<IActionResult> Category(string slug, int page = 1)
    {
        const int pageSize = 10;
        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Slug == slug);

        var query = _db.Articles
            .Include(a => a.Category).Include(a => a.Author)
            .Where(a => a.Status == "published");

        if (cat != null)
            query = query.Where(a => a.CategoryId == cat.Id);

        var total = await query.CountAsync();
        var arts  = await query
            .OrderByDescending(a => a.PublishedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var vm = new CategoryViewModel
        {
            Category      = cat,
            Articles      = arts,
            AllCategories = await _db.Categories.Where(c => c.IsVisible).OrderBy(c => c.SortOrder).ToListAsync(),
            TotalCount    = total,
            Page          = page,
            PageSize      = pageSize
        };

        return View(vm);
    }

    public async Task<IActionResult> Search(string q = "")
    {
        var results = new List<Article>();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var kw = q.ToLower();
            results = await _db.Articles
                .Include(a => a.Category)
                .Where(a => a.Status == "published" &&
                    (a.Title.ToLower().Contains(kw) ||
                     a.Summary.ToLower().Contains(kw) ||
                     a.Content.ToLower().Contains(kw)))
                .OrderByDescending(a => a.PublishedAt)
                .Take(20).ToListAsync();
        }

        return View(new SearchViewModel { Keyword = q, Results = results, TotalCount = results.Count });
    }

    /// <summary>
    /// [H] Lịch sử bài viết đã xem theo session — route /lich-su.
    /// </summary>
    [Route("/lich-su")]
    public async Task<IActionResult> ViewHistory()
    {
        ViewData["ActiveNav"] = "lich-su";
        ViewData["Title"]     = "Lịch sử đã xem";

        try
        {
            HttpContext.Session.SetString("__init", "1");
            var sid = HttpContext.Session.Id;
            if (string.IsNullOrEmpty(sid))
                return View(new List<ViewHistoryItem>());

            var histories = await _db.ViewHistories
                .Where(v => v.SessionId == sid)
                .OrderByDescending(v => v.ViewedAt)
                .Take(20)
                .ToListAsync();

            if (histories.Count == 0) return View(new List<ViewHistoryItem>());

            var articleIds = histories.Select(h => h.ArticleId).Distinct().ToList();
            var articles = await _db.Articles
                .Include(a => a.Category)
                .Where(a => articleIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id);

            var items = histories
                .Where(h => articles.ContainsKey(h.ArticleId))
                .Select(h => new ViewHistoryItem
                {
                    History = h,
                    Article = articles[h.ArticleId]
                })
                .ToList();

            return View(items);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ViewHistory page failed");
            return View(new List<ViewHistoryItem>());
        }
    }
}