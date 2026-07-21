using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using System.Text;
using System.Xml;

namespace WisdomITNews.Controllers;

public class RssController : Controller
{
    private readonly AppDbContext _db;

    public RssController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// RSS Feed chính — /rss
    /// Hỗ trợ: /rss?region=ha-noi hoặc /rss?category=lap-trinh
    /// </summary>
    [Route("/rss")]
    [ResponseCache(Duration = 600)] // cache 10 phút
    // Đây là luồng xử lý xuất RSS feed của site
    // Luồng: 1) Lấy bài published (lọc theo region/category nếu có), 30 bài mới nhất
    //        2) Dựng XML RSS 2.0 (title/link/description/pubDate/thumbnail từng bài)
    //        3) Trả về nội dung application/rss+xml (cache 10 phút)
    // Bảng: Articles, Categories
    public async Task<IActionResult> Feed(string? region, string? category)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        // Query bài published
        var query = _db.Articles
            .Include(a => a.Category)
            .Where(a => a.Status == "published");

        // Lọc theo vùng nếu có
        string feedTitle = "Wisdom IT News";
        string feedDesc = "Tin tức công nghệ, AI & lập trình mới nhất";

        if (!string.IsNullOrWhiteSpace(region))
        {
            query = query.Where(a => a.Region == region);
            feedTitle += $" — {RegionName(region)}";
            feedDesc = $"Tin tức IT khu vực {RegionName(region)}";
        }

        // Lọc theo category nếu có
        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Slug == category);
            if (cat != null)
            {
                query = query.Where(a => a.CategoryId == cat.Id);
                feedTitle += $" — {cat.Name}";
                feedDesc = $"Tin tức {cat.Name} mới nhất";
            }
        }

        var articles = await query
            .OrderByDescending(a => a.PublishedAt)
            .Take(30)
            .ToListAsync();

        // Tạo XML
        var sb = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = Encoding.UTF8
        };

        using (var writer = XmlWriter.Create(sb, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("rss");
            writer.WriteAttributeString("version", "2.0");
            writer.WriteAttributeString("xmlns", "atom", null, "http://www.w3.org/2005/Atom");

            writer.WriteStartElement("channel");

            // Channel info
            writer.WriteElementString("title", feedTitle);
            writer.WriteElementString("link", baseUrl);
            writer.WriteElementString("description", feedDesc);
            writer.WriteElementString("language", "vi-VN");
            writer.WriteElementString("copyright", $"© {DateTime.Now.Year} Wisdom IT News");
            writer.WriteElementString("lastBuildDate", DateTime.Now.ToString("R"));
            writer.WriteElementString("generator", "WisdomITNews RSS");

            // Atom self link
            var selfUrl = $"{baseUrl}/rss";
            if (!string.IsNullOrWhiteSpace(region)) selfUrl += $"?region={region}";
            if (!string.IsNullOrWhiteSpace(category))
                selfUrl += (selfUrl.Contains('?') ? "&" : "?") + $"category={category}";

            writer.WriteStartElement("atom", "link", "http://www.w3.org/2005/Atom");
            writer.WriteAttributeString("href", selfUrl);
            writer.WriteAttributeString("rel", "self");
            writer.WriteAttributeString("type", "application/rss+xml");
            writer.WriteEndElement();

            // Logo
            writer.WriteStartElement("image");
            writer.WriteElementString("url", $"{baseUrl}/images/logo.png");
            writer.WriteElementString("title", feedTitle);
            writer.WriteElementString("link", baseUrl);
            writer.WriteEndElement();

            // Các bài viết
            foreach (var a in articles)
            {
                writer.WriteStartElement("item");

                writer.WriteElementString("title", a.Title);
                writer.WriteElementString("link", $"{baseUrl}/bai-viet/{a.Slug}");
                writer.WriteElementString("description", CleanHtml(a.Summary));

                // Category
                if (a.Category != null)
                    writer.WriteElementString("category", a.Category.Name);

                // GUID
                writer.WriteStartElement("guid");
                writer.WriteAttributeString("isPermaLink", "true");
                writer.WriteString($"{baseUrl}/bai-viet/{a.Slug}");
                writer.WriteEndElement();

                // Ngày đăng
                if (a.PublishedAt.HasValue)
                    writer.WriteElementString("pubDate", a.PublishedAt.Value.ToString("R"));

                // Thumbnail
                if (!string.IsNullOrEmpty(a.Thumbnail))
                {
                    var thumbUrl = a.Thumbnail.StartsWith("http")
                        ? a.Thumbnail
                        : $"{baseUrl}{a.Thumbnail}";
                    writer.WriteStartElement("enclosure");
                    writer.WriteAttributeString("url", thumbUrl);
                    writer.WriteAttributeString("type", "image/jpeg");
                    writer.WriteAttributeString("length", "0");
                    writer.WriteEndElement();
                }

                writer.WriteEndElement(); // item
            }

            writer.WriteEndElement(); // channel
            writer.WriteEndElement(); // rss
            writer.WriteEndDocument();
        }

        return Content(sb.ToString(), "application/rss+xml; charset=utf-8");
    }

    /// <summary>
    /// Trang hiển thị danh sách RSS feeds — /rss/list
    /// </summary>
    [Route("/rss/list")]
    // Đây là luồng xử lý hiển thị trang danh sách các RSS feed
    public async Task<IActionResult> List()
    {
        ViewData["Title"] = "RSS Feeds";
        var categories = await _db.Categories
            .Where(c => c.IsVisible)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();
        return View(categories);
    }

    // ===== SITEMAP =====
    // Đây là luồng xử lý tạo sitemap.xml cho SEO
    // Luồng: lấy trang chủ + danh mục + 2000 bài published mới nhất -> dựng XML urlset
    [Route("/sitemap.xml")]
    public async Task<IActionResult> Sitemap()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var arts = await _db.Articles
            .Where(a => a.Status == "published")
            .OrderByDescending(a => a.UpdatedAt)
            .Select(a => new { a.Slug, a.UpdatedAt })
            .Take(2000).ToListAsync();
        var cats = await _db.Categories.Where(c => c.IsVisible).Select(c => c.Slug).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        sb.AppendLine($"<url><loc>{baseUrl}/</loc><changefreq>hourly</changefreq><priority>1.0</priority></url>");
        foreach (var c in cats)
            sb.AppendLine($"<url><loc>{baseUrl}/danh-muc/{c}</loc><changefreq>daily</changefreq><priority>0.7</priority></url>");
        foreach (var a in arts)
            sb.AppendLine($"<url><loc>{baseUrl}/bai-viet/{a.Slug}</loc><lastmod>{a.UpdatedAt:yyyy-MM-dd}</lastmod><changefreq>weekly</changefreq><priority>0.8</priority></url>");
        sb.AppendLine("</urlset>");
        return Content(sb.ToString(), "application/xml; charset=utf-8");
    }

    // Đây là luồng xử lý trả về robots.txt (khai báo Sitemap cho bot tìm kiếm)
    [Route("/robots.txt")]
    public IActionResult Robots()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var txt = $"User-agent: *\nAllow: /\nSitemap: {baseUrl}/sitemap.xml\n";
        return Content(txt, "text/plain; charset=utf-8");
    }

    // Đây là luồng xử lý làm sạch HTML cho mô tả RSS (bỏ thẻ, gộp khoảng trắng, cắt 300 ký tự)
    private static string CleanHtml(string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        // Loại bỏ HTML tags
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        // Giới hạn 300 ký tự
        return text.Length > 300 ? text[..300] + "..." : text;
    }

    // Đây là luồng xử lý đổi slug vùng sang tên hiển thị (cho tiêu đề feed)
    private static string RegionName(string slug) => slug switch
    {
        "dong-nai" => "Đồng Nai",
        "ha-noi" => "Hà Nội",
        "ho-chi-minh" => "TP. Hồ Chí Minh",
        "da-nang" => "Đà Nẵng",
        "hai-phong" => "Hải Phòng",
        "can-tho" => "Cần Thơ",
        _ => slug
    };
}