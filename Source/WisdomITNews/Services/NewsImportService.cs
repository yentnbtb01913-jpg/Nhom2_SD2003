using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Xml.Linq;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Services;

// Nhập tin từ RSS theo MÔ HÌNH TỔNG HỢP: lưu phần mô tả mà nhà cung cấp ĐÃ syndicate
// trong feed (nhiều đoạn), KÈM ghi nguồn + link bài gốc để đọc toàn văn.
// KHÔNG cào toàn bộ nội dung gốc -> không vi phạm bản quyền.
public class NewsImportService
{
    private readonly AppDbContext _db;
    private readonly ILogger<NewsImportService> _logger;
    private static readonly HttpClient _http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip
                                   | System.Net.DecompressionMethods.Deflate
                                   | System.Net.DecompressionMethods.Brotli
        };

        var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language",
            "vi-VN,vi;q=0.9,en-US;q=0.8,en;q=0.7");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding",
            "gzip, deflate, br");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Connection", "keep-alive");
        return client;
    }

    public NewsImportService(AppDbContext db, ILogger<NewsImportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Trả về (thêm mới, cập nhật, bỏ qua)
    public async Task<(int added, int updated, int skipped)> ImportRssAsync(string feedUrl, string sourceName, int? categoryId, int max = 30)
    {
        int added = 0, updated = 0, skipped = 0;

        string xml;
        try
        {
            xml = await _http.GetStringAsync(feedUrl);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Tải RSS thất bại [{StatusCode}]: {Url}",
                ex.StatusCode, feedUrl);
            return (0, 0, 0);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "RSS timeout sau 30s: {Url}", feedUrl);
            return (0, 0, 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi không xác định khi tải RSS: {Url}", feedUrl);
            return (0, 0, 0);
        }

        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch (Exception ex) { _logger.LogWarning(ex, "Parse RSS thất bại: {Url}", feedUrl); return (0, 0, 0); }

        foreach (var it in doc.Descendants("item").Take(max))
        {
            var link = ((string?)it.Element("link"))?.Trim();
            var title = ((string?)it.Element("title"))?.Trim();
            if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(title)) continue;

            // Một số feed để nội dung đầy đủ hơn ở content:encoded; ưu tiên cái dài hơn
            XNamespace cns = "http://purl.org/rss/1.0/modules/content/";
            var contentEncoded = (string?)it.Element(cns + "encoded");
            var descRaw = (string?)it.Element("description") ?? "";
            var raw = (contentEncoded != null && contentEncoded.Length > descRaw.Length) ? contentEncoded : descRaw;

            var paras = ToParagraphs(raw);
            var plain = string.Join(" ", paras);
            var summaryShort = plain.Length > 280 ? plain.Substring(0, 280).TrimEnd() + "…" : plain;
            if (string.IsNullOrWhiteSpace(summaryShort)) summaryShort = title;

            var img = GetThumbnail(it);
            var bodyHtml = BuildBody(paras, sourceName, link);

            var published = DateTime.Now;
            var pd = (string?)it.Element("pubDate");
            if (!string.IsNullOrWhiteSpace(pd) &&
                DateTimeOffset.TryParse(pd, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dto))
                published = dto.LocalDateTime;

            // Đã có bài cùng link gốc?
            var existing = await _db.Articles.FirstOrDefaultAsync(a => a.SourceUrl == link);
            if (existing != null)
            {
                if (existing.IsExternal)   // làm mới bài tổng hợp cũ (để bài ngắn trước đây dài ra)
                {
                    existing.Summary = summaryShort;
                    existing.Content = bodyHtml;
                    if (!string.IsNullOrWhiteSpace(img)) existing.Thumbnail = img;
                    existing.UpdatedAt = DateTime.Now;
                    updated++;
                }
                else skipped++;
                continue;
            }

            var slug = SlugHelper.MakeSlug(title);
            if (string.IsNullOrWhiteSpace(slug)) slug = "tin-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            if (await _db.Articles.AnyAsync(a => a.Slug == slug)) slug += "-" + DateTimeOffset.Now.ToUnixTimeSeconds();

            _db.Articles.Add(new Article
            {
                Title = title,
                Slug = slug,
                Summary = summaryShort,
                Content = bodyHtml,
                Thumbnail = string.IsNullOrWhiteSpace(img) ? null : img,
                CategoryId = categoryId,
                Status = "published",
                IsExternal = true,
                SourceName = sourceName,
                SourceUrl = link,
                PublishedAt = published,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });
            added++;
        }

        if (added > 0 || updated > 0) await _db.SaveChangesAsync();
        return (added, updated, skipped);
    }

    // Bỏ tag, decode, tách thành các đoạn văn không rỗng
    private static List<string> ToParagraphs(string raw)
    {
        var noTag = System.Text.RegularExpressions.Regex.Replace(raw, "<[^>]+>", "\n");
        var dec = System.Net.WebUtility.HtmlDecode(noTag).Replace(' ', ' ');
        var list = new List<string>();
        foreach (var part in dec.Split('\n'))
        {
            var t = System.Text.RegularExpressions.Regex.Replace(part, @"[ \t]+", " ").Trim();
            if (t.Length > 0) list.Add(t);
        }
        return list;
    }

    // Ghép các đoạn thành HTML + đoạn dẫn nguồn (Content được render bằng Html.Raw)
    private static string BuildBody(List<string> paras, string sourceName, string sourceUrl)
    {
        var sb = new StringBuilder();
        foreach (var p in paras)
            sb.Append("<p>").Append(System.Net.WebUtility.HtmlEncode(p)).Append("</p>");
        sb.Append("<p style=\"margin-top:18px;padding:12px 14px;background:#eff6ff;border-left:4px solid #1e3a8a;border-radius:8px;font-size:14px;\">")
          .Append("📌 Đây là <b>bài tổng hợp</b> từ <b>").Append(System.Net.WebUtility.HtmlEncode(sourceName)).Append("</b>. ")
          .Append("Đọc <b>toàn văn</b> tại nguồn: <a href=\"").Append(System.Net.WebUtility.HtmlEncode(sourceUrl))
          .Append("\" target=\"_blank\" rel=\"noopener noreferrer\">").Append(System.Net.WebUtility.HtmlEncode(sourceUrl)).Append("</a>.</p>");
        return sb.ToString();
    }

    // Lấy thumbnail theo thứ tự ưu tiên: enclosure(image) → media:content → media:thumbnail → regex từ description
    private static string? GetThumbnail(XElement item)
    {
        // 1. enclosure với type="image/..."
        var enclosure = item.Element("enclosure");
        if (enclosure != null)
        {
            var type = enclosure.Attribute("type")?.Value;
            var url = enclosure.Attribute("url")?.Value;
            if (!string.IsNullOrWhiteSpace(type) && type.StartsWith("image/") && !string.IsNullOrWhiteSpace(url))
                return url;
        }

        // 2. media:content
        XNamespace media = "http://search.yahoo.com/mrss/";
        var mediaContent = item.Element(media + "content");
        if (mediaContent != null)
        {
            var url = mediaContent.Attribute("url")?.Value;
            if (!string.IsNullOrWhiteSpace(url))
                return url;
        }

        // 3. media:thumbnail
        var mediaThumbnail = item.Element(media + "thumbnail");
        if (mediaThumbnail != null)
        {
            var url = mediaThumbnail.Attribute("url")?.Value;
            if (!string.IsNullOrWhiteSpace(url))
                return url;
        }

        // 4. Regex extract <img src> từ description hoặc content:encoded
        XNamespace cns = "http://purl.org/rss/1.0/modules/content/";
        var contentEncoded = (string?)item.Element(cns + "encoded");
        var descRaw = (string?)item.Element("description") ?? "";
        var contentToSearch = (contentEncoded != null && contentEncoded.Length > descRaw.Length) ? contentEncoded : descRaw;

        if (!string.IsNullOrWhiteSpace(contentToSearch))
        {
            var imgMatch = System.Text.RegularExpressions.Regex.Match(contentToSearch, @"<img[^>]+src=[""']([^""']+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (imgMatch.Success && !string.IsNullOrWhiteSpace(imgMatch.Groups[1].Value))
                return imgMatch.Groups[1].Value;
        }

        return null;
    }

    // Import từ RssSource object
    public async Task<(int added, int updated, int skipped)> ImportFromSourceAsync(RssSource source)
    {
        var result = await ImportRssAsync(source.FeedUrl, source.Name, source.DefaultCategoryId, source.MaxImport);
        source.LastImportAt = DateTime.Now;
        source.TotalImported += result.added;
        return result;
    }
}
