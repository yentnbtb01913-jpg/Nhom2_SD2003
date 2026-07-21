using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;
using WisdomITNews.Services;

namespace WisdomITNews.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<HomeController> _logger;
    private readonly FeaturedArticleService _featured;
    public HomeController(AppDbContext db, ILogger<HomeController> logger, FeaturedArticleService featured)
    {
        _db = db;
        _logger = logger;
        _featured = featured;
    }

    // Đây là luồng xử lý hiển thị trang chủ
    // Luồng: 1) Đọc vùng hiện tại từ session (ưu tiên bài cùng vùng, thiếu thì bù bài chung)
    //        2) Nạp các khu: Tin nổi bật tự động (Top 4), Mới nhất (12), AI (danh mục 3),
    //           Nóng 24H (theo ViewHistory 24 giờ, thiếu thì bù theo tổng Views)
    //        3) Nạp danh mục + tag -> HomeViewModel
    // Bảng: Articles, ViewHistories, Categories, Tags
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

        // Tin nổi bật tự động (khu vực lớn Trang chủ) — TOP 4 theo Ghim > Lượt xem > Ngày đăng > Ngày cập nhật,
        // tính lúc đọc (không lưu vị trí cố định) nên tự đồng bộ ngay khi lượt xem thay đổi.
        var featuredRegion = await _featured.GetHomepageTop4Async();

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

    // Đây là luồng xử lý hiển thị trang danh mục (chuyên mục)
    // Luồng: tìm Category theo slug -> lấy bài published của danh mục (phân trang 10) -> CategoryViewModel
    // Bảng: Articles, Categories
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

    // ===== Tìm kiếm thông minh (lịch sử + gợi ý real-time) =====
    private const int HistoryLimit = 10;
    private const int PopularKeywordLimit = 8;
    private const int SuggestArticleLimit = 8;
    private const int SuggestCategoryLimit = 5;
    private const int SuggestKeywordLimit = 6;
    private static readonly string[] FallbackPopularKeywords =
        { "AI", "ChatGPT", "Windows", "Linux", "Python", "Gemini", "Claude", "Cyber Security" };

    /// <summary>Bỏ dấu tiếng Việt + hạ chữ thường để so khớp không phân biệt hoa/thường và dấu.</summary>
    private static string RemoveDiacritics(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC)
            .Replace('đ', 'd').Replace('Đ', 'D');
    }

    private static string NormalizeKeyword(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var noDiacritics = RemoveDiacritics(s).ToLowerInvariant().Trim();
        return System.Text.RegularExpressions.Regex.Replace(noDiacritics, @"\s+", " ");
    }

    /// <summary>Xác định chủ sở hữu lịch sử tìm kiếm: UserId (nếu đăng nhập) + SessionId (luôn có, kể cả khách).</summary>
    private (int? userId, string sessionId) GetSearchOwner()
    {
        HttpContext.Session.SetString("__init", "1"); // đảm bảo session đã được khởi tạo → có Session.Id ổn định
        var userId = HttpContext.Session.GetInt32("UserId");
        var sessionId = HttpContext.Session.Id;
        return (userId, sessionId);
    }

    // Đây là luồng xử lý lưu lịch sử tìm kiếm
    // Luồng: chuẩn hóa từ khóa -> trùng thì cập nhật thời gian, chưa có thì thêm mới
    //        -> giới hạn tối đa 50 mục/người-phiên (xóa bớt cũ)
    // Bảng: SearchHistories
    private async Task SaveSearchHistoryAsync(string keyword)
    {
        var norm = NormalizeKeyword(keyword);
        if (string.IsNullOrEmpty(norm)) return;

        var (userId, sessionId) = GetSearchOwner();
        var mine = await _db.SearchHistories
            .Where(h => h.SessionId == sessionId && h.UserId == userId)
            .OrderByDescending(h => h.SearchedAt)
            .ToListAsync();

        var dup = mine.FirstOrDefault(h => NormalizeKeyword(h.Keyword) == norm);
        if (dup != null)
        {
            dup.Keyword = keyword.Trim();
            dup.SearchedAt = DateTime.Now;
        }
        else
        {
            _db.SearchHistories.Add(new SearchHistory
            {
                UserId = userId,
                SessionId = sessionId,
                Keyword = keyword.Trim(),
                SearchedAt = DateTime.Now
            });
        }
        await _db.SaveChangesAsync();

        // Giới hạn tối đa 50 mục lưu trữ mỗi người dùng/phiên để tránh phình bảng
        var all = await _db.SearchHistories
            .Where(h => h.SessionId == sessionId && h.UserId == userId)
            .OrderByDescending(h => h.SearchedAt)
            .ToListAsync();
        if (all.Count > 50)
        {
            _db.SearchHistories.RemoveRange(all.Skip(50));
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>Danh sách bài published khớp từ khóa theo Title/Summary/Content/Tag/Category (fuzzy, không phân biệt hoa-thường/dấu).</summary>
    // Đây là luồng xử lý tìm bài theo từ khóa (lõi tìm kiếm)
    // Luồng: 1) Khớp Title/Summary/Content/Category/Tag (chứa từ khóa)
    //        2) Chưa đủ -> quét gần đúng BỎ DẤU trên 400 bài gần đây (bắt trường hợp gõ không dấu)
    // Bảng: Articles, Tags, Categories
    private async Task<List<Article>> SearchArticlesAsync(string q, int take)
    {
        var kwLower = q.Trim().ToLower();
        var kwNorm = NormalizeKeyword(q);

        var matchingTagIds = (await _db.Tags.ToListAsync())
            .Where(t => NormalizeKeyword(t.Name).Contains(kwNorm))
            .Select(t => t.Id)
            .ToHashSet();

        var dbMatches = await _db.Articles
            .Include(a => a.Category)
            .Where(a => a.Status == "published" && (
                a.Title.ToLower().Contains(kwLower) ||
                a.Summary.ToLower().Contains(kwLower) ||
                a.Content.ToLower().Contains(kwLower) ||
                (a.Category != null && a.Category.Name.ToLower().Contains(kwLower)) ||
                a.ArticleTags.Any(at => matchingTagIds.Contains(at.TagId))))
            .OrderByDescending(a => a.PublishedAt)
            .Take(take)
            .ToListAsync();

        if (dbMatches.Count >= take) return dbMatches;

        // Vòng bổ sung: quét gần đúng (bỏ dấu) trên tập bài gần đây để bắt các trường hợp gõ không dấu
        var existIds = dbMatches.Select(a => a.Id).ToHashSet();
        var candidates = await _db.Articles
            .Include(a => a.Category)
            .Where(a => a.Status == "published" && !existIds.Contains(a.Id))
            .OrderByDescending(a => a.PublishedAt)
            .Take(400)
            .ToListAsync();

        var fuzzy = candidates.Where(a =>
                NormalizeKeyword(a.Title).Contains(kwNorm) ||
                NormalizeKeyword(a.Summary).Contains(kwNorm) ||
                NormalizeKeyword(a.Category?.Name).Contains(kwNorm))
            .Take(take - dbMatches.Count);

        dbMatches.AddRange(fuzzy);
        return dbMatches;
    }

    /// <summary>Trạng thái rỗng của ô tìm kiếm: lịch sử gần đây + từ khóa phổ biến.</summary>
    // Đây là luồng xử lý hiển thị ô tìm kiếm khi rỗng (lịch sử gần đây + từ khóa phổ biến)
    [HttpGet]
    public async Task<IActionResult> SearchPanel()
    {
        var (userId, sessionId) = GetSearchOwner();

        var history = await _db.SearchHistories
            .Where(h => h.SessionId == sessionId && h.UserId == userId)
            .OrderByDescending(h => h.SearchedAt)
            .Take(HistoryLimit)
            .Select(h => new { id = h.Id, keyword = h.Keyword })
            .ToListAsync();

        var grouped = await _db.SearchHistories
            .GroupBy(h => h.Keyword.Trim().ToLower())
            .Select(g => new { key = g.Key, count = g.Count(), sample = g.Select(x => x.Keyword).FirstOrDefault() })
            .OrderByDescending(g => g.count)
            .Take(PopularKeywordLimit)
            .ToListAsync();

        var popular = grouped.Select(g => g.sample).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (popular.Count < PopularKeywordLimit)
        {
            foreach (var fb in FallbackPopularKeywords)
            {
                if (popular.Count >= PopularKeywordLimit) break;
                if (!popular.Any(p => NormalizeKeyword(p) == NormalizeKeyword(fb))) popular.Add(fb);
            }
        }

        return Json(new { history, popular });
    }

    // Gợi ý tìm kiếm real-time (autocomplete) — trả JSON: từ khóa gợi ý, danh mục liên quan, bài viết liên quan
    // Đây là luồng xử lý gợi ý tìm kiếm real-time (autocomplete)
    // Luồng: theo từ khóa -> trả từ khóa gợi ý + danh mục liên quan + bài viết liên quan (JSON)
    [HttpGet]
    public async Task<IActionResult> SearchSuggest(string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Json(new { keywords = new string[0], categories = new object[0], articles = new object[0] });

        var kwNorm = NormalizeKeyword(q);

        var categories = (await _db.Categories.Where(c => c.IsVisible).ToListAsync())
            .Where(c => NormalizeKeyword(c.Name).Contains(kwNorm) || NormalizeKeyword(c.Slug).Contains(kwNorm))
            .Take(SuggestCategoryLimit)
            .Select(c => new { name = c.Name, slug = c.Slug, icon = c.Icon })
            .ToList();

        var matchingTags = (await _db.Tags.ToListAsync())
            .Where(t => NormalizeKeyword(t.Name).Contains(kwNorm))
            .Select(t => t.Name)
            .ToList();

        var articles = (await SearchArticlesAsync(q, SuggestArticleLimit))
            .Select(a => new
            {
                title = a.Title,
                slug = a.Slug,
                thumbnail = a.Thumbnail,
                categoryName = a.Category != null ? a.Category.Name : ""
            })
            .ToList();

        var keywordSuggestions = new List<string>();
        keywordSuggestions.AddRange(categories.Select(c => c.name));
        keywordSuggestions.AddRange(matchingTags);
        keywordSuggestions.AddRange(articles.Select(a => a.title));
        var keywords = keywordSuggestions
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .GroupBy(k => NormalizeKeyword(k)).Select(g => g.First())
            .Take(SuggestKeywordLimit)
            .ToList();

        return Json(new { keywords, categories, articles });
    }

    /// <summary>Xóa một từ khóa khỏi lịch sử tìm kiếm (chỉ xóa đúng chủ sở hữu).</summary>
    // Đây là luồng xử lý xóa 1 từ khóa khỏi lịch sử tìm kiếm (đúng chủ sở hữu)
    [HttpPost]
    public async Task<IActionResult> DeleteSearchHistory(int id)
    {
        var (userId, sessionId) = GetSearchOwner();
        var item = await _db.SearchHistories.FirstOrDefaultAsync(h => h.Id == id && h.SessionId == sessionId && h.UserId == userId);
        if (item == null) return Json(new { success = false });

        _db.SearchHistories.Remove(item);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    /// <summary>Xóa toàn bộ lịch sử tìm kiếm của người dùng/phiên hiện tại.</summary>
    // Đây là luồng xử lý xóa toàn bộ lịch sử tìm kiếm của người/phiên hiện tại
    [HttpPost]
    public async Task<IActionResult> ClearSearchHistory()
    {
        var (userId, sessionId) = GetSearchOwner();
        var mine = _db.SearchHistories.Where(h => h.SessionId == sessionId && h.UserId == userId);
        _db.SearchHistories.RemoveRange(mine);
        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // Đây là luồng xử lý trang kết quả tìm kiếm đầy đủ (/tim-kiem)
    // Luồng: có từ khóa -> lưu lịch sử + tìm 30 bài -> SearchViewModel
    public async Task<IActionResult> Search(string q = "")
    {
        var results = new List<Article>();
        if (!string.IsNullOrWhiteSpace(q))
        {
            await SaveSearchHistoryAsync(q);
            results = await SearchArticlesAsync(q, 30);
        }

        return View(new SearchViewModel { Keyword = q, Results = results, TotalCount = results.Count });
    }

    /// <summary>
    /// [H] Lịch sử bài viết đã xem theo session — route /lich-su.
    /// </summary>
    // Đây là luồng xử lý hiển thị lịch sử bài đã xem (theo SessionId)
    // Luồng: lấy 20 ViewHistory gần nhất của phiên -> ghép Article -> danh sách ViewHistoryItem
    // Bảng: ViewHistories, Articles
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

    // Tự nạp thêm bài (infinite scroll) + chèn 1 video ngẫu nhiên như một tin
    // Đây là luồng xử lý nạp thêm bài (cuộn vô hạn)
    // Luồng: lấy 6 bài tiếp theo + chèn 1 video ngẫu nhiên xen kẽ -> partial "_FeedMore"
    // Bảng: Articles, Videos
    public async Task<IActionResult> LoadMore(int skip = 0)
    {
        const int take = 6;
        var arts = await _db.Articles
            .Include(a => a.Category)
            .Where(a => a.Status == "published")
            .OrderByDescending(a => a.PublishedAt)
            .Skip(skip).Take(take)
            .ToListAsync();

        // Lấy 1 video ngẫu nhiên (trong 30 video mới nhất) để chèn xen kẽ
        Models.Video? vid = null;
        if (arts.Count > 0)
        {
            var vidCount = await _db.Videos.CountAsync();
            if (vidCount > 0)
            {
                int pool = Math.Min(vidCount, 30);
                int idx = new Random().Next(pool);
                vid = await _db.Videos
                    .OrderByDescending(v => v.PublishedAt)
                    .Skip(idx).Take(1).FirstOrDefaultAsync();
            }
        }
        ViewBag.Video = vid;
        ViewBag.InsertAt = arts.Count > 1 ? new Random().Next(1, arts.Count) : arts.Count;
        return PartialView("_FeedMore", arts);
    }
}