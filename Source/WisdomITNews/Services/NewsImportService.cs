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
    private static readonly HttpClient _http = new HttpClient();

    public NewsImportService(AppDbContext db, ILogger<NewsImportService> logger)
    {
        _db = db;
        _logger = logger;
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("WisdomITNews/1.0 (+rss-aggregator)");
    }

    // Trả về (thêm mới, cập nhật, bỏ qua)
    public async Task<(int added, int updated, int skipped)> ImportRssAsync(string feedUrl, string sourceName, int? categoryId, int max = 30)
    {
        int added = 0, updated = 0, skipped = 0;

        string xml;
        try { xml = await _http.GetStringAsync(feedUrl); }
        catch (Exception ex) { _logger.LogWarning(ex, "Tải RSS thất bại: {Url}", feedUrl); return (0, 0, 0); }

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

            var img = (string?)it.Element("enclosure")?.Attribute("url");
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
}
