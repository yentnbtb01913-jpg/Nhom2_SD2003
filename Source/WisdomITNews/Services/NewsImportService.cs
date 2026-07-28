using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Xml.Linq;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Services;

// Nhập tin từ RSS. Với mỗi bài: lấy TOÀN VĂN từ trang gốc bằng Readability (SmartReader),
// nếu bóc được thì lưu bài đầy đủ; nếu thất bại thì giữ excerpt trong feed.
// LUÔN ghi rõ nguồn (SourceName) + link bài gốc (SourceUrl) trên trang chi tiết.
// (Dùng cho dự án tốt nghiệp/học tập; tôn trọng bản quyền bằng cách dẫn nguồn đầy đủ.)
public class NewsImportService
{
    private readonly AppDbContext _db;
    private readonly ILogger<NewsImportService> _logger;
    private readonly AIService _ai;
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

    public NewsImportService(AppDbContext db, ILogger<NewsImportService> logger, AIService ai)
    {
        _db = db;
        _logger = logger;
        _ai = ai;
    }

    // Đây là luồng xử lý nhập tin từ RSS
    // Luồng: 1) Tải XML feed (HttpClient giả UA, timeout 30s); lỗi -> connected=false
    //        2) Parse từng <item>: title, link, mô tả/content:encoded, ảnh, ngày đăng
    //        3) Trùng SourceUrl? onlyNew -> bỏ qua; bài IsExternal cũ -> làm mới nội dung
    //        4) Tự phân loại danh mục: từ khóa (MatchCategory) -> AI ClassifyCategory -> fallback
    //        5) Tạo Article IsExternal=true, Status="published", ghi nguồn + link gốc
    //        6) Lưu DB; dừng khi đủ maxNew. Trả (connected, added, updated, skipped)
    // Bảng: Articles, Categories, RssSources, AILogs
    // Trả về (connected, thêm mới, cập nhật, bỏ qua). connected=false nghĩa là lỗi kết nối/parse nguồn.
    // maxNew: giới hạn số bài MỚI thêm. onlyNew: chỉ nhập bài chưa có (bỏ qua, không cập nhật bài cũ).
    // delayPerArticleMs: nghỉ giữa xử lý từng bài.
    public async Task<(bool connected, int added, int updated, int skipped)> ImportRssAsync(
        string feedUrl, string sourceName, int? categoryId, int max = 30,
        int maxNew = int.MaxValue, bool onlyNew = false, int delayPerArticleMs = 0, string? importedBy = null,
        bool fullText = true)
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
            return (false, 0, 0, 0);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "RSS timeout sau 30s: {Url}", feedUrl);
            return (false, 0, 0, 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi không xác định khi tải RSS: {Url}", feedUrl);
            return (false, 0, 0, 0);
        }

        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch (Exception ex) { _logger.LogWarning(ex, "Parse RSS thất bại: {Url}", feedUrl); return (false, 0, 0, 0); }

        // Danh mục có sẵn — dùng để tự phân loại từng bài (từ khóa trước, AI khi không khớp)
        var cats = await _db.Categories.Where(c => c.IsVisible).ToListAsync();

        foreach (var it in doc.Descendants("item").Take(max))
        {
            var link = ((string?)it.Element("link"))?.Trim();
            var rssCats = it.Elements("category").Select(e => (e.Value ?? "").Trim()).Where(s => s.Length > 0).ToList();
            // Decode thực thể HTML trong tiêu đề (&uacute; &#039; &ecirc; ...) -> chữ thuần
            var title = System.Net.WebUtility.HtmlDecode(((string?)it.Element("title"))?.Trim() ?? "").Trim();
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

            // Lấy TOÀN VĂN từ trang gốc bằng Readability (SmartReader). Thành công -> thay nội dung ngắn
            // bằng bài đầy đủ; thất bại (bị chặn/không đọc được/quá ngắn) -> giữ nguyên excerpt của feed.
            // Vẫn LƯU SourceName + SourceUrl để trang chi tiết ghi rõ nguồn và link bài gốc.
            if (fullText)
            {
                var full = await TryExtractFullAsync(link);
                if (full != null)
                {
                    bodyHtml = full.Value.bodyHtml;
                    var fp = full.Value.plain;
                    if (!string.IsNullOrWhiteSpace(fp))
                        summaryShort = fp.Length > 280 ? fp.Substring(0, 280).TrimEnd() + "…" : fp;
                    if (string.IsNullOrWhiteSpace(img) && !string.IsNullOrWhiteSpace(full.Value.image))
                        img = full.Value.image;
                }
            }

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
                if (onlyNew) { skipped++; continue; }   // chỉ nhập bài mới -> bỏ qua bài đã có
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

            // Tự phân loại vào danh mục có sẵn: từ khóa/RSS-category trước, không khớp thì AI, cuối cùng fallback danh mục nguồn
            // ^ Chú thích tổng: nêu rõ thứ tự ưu tiên 3 tầng (nhãn/từ khóa -> AI -> danh mục mặc định của nguồn).

            // [ĐÃ GỠ AI PHÂN LOẠI + ÁNH XẠ + TỰ HỌC]
            // Bài nhập về vào THẲNG danh mục mà admin đã gán cho nguồn RSS (DefaultCategoryId).
            // Lý do: khi thêm nguồn ta đã biết nguồn nói về gì -> gán danh mục cố định, không cần AI.
            int? catId = categoryId;




            // tạo slug từ tiêu đề (SlugHelper.MakeSlug) để dùng trong URL. Nếu trùng slug thì thêm timestamp.
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
                CategoryId = catId,
                ImportedBy = string.IsNullOrEmpty(importedBy) ? $"Tự động · {sourceName}" : $"{importedBy} · {sourceName}",
                Status = "published",
                IsExternal = true,
                SourceName = sourceName,
                SourceUrl = link,
                PublishedAt = published,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });
            added++;
            if (delayPerArticleMs > 0) await Task.Delay(delayPerArticleMs);   // #4 nghỉ giữa mỗi bài
            if (added >= maxNew) break;   // đủ số bài mới cần
        }

        if (added > 0 || updated > 0) await _db.SaveChangesAsync();
        return (true, added, updated, skipped);
    }

    // Đây là luồng xử lý nhập 1 bài mới từ một nguồn RSS
    // Nhập TỐI ĐA 1 bài mới từ 1 nguồn (dùng cho tự động 1 bài/phút).
    public async Task<int> ImportOneFromSourceAsync(RssSource source)
    {
        if (source.SourceType == "video")
        {
            var rv = await ImportVideoRssAsync(source, maxNew: 1);
            source.LastImportAt = DateTime.Now;
            source.TotalImported += rv.added;
            return rv.added;
        }
        var r = await ImportRssAsync(source.FeedUrl, source.Name, source.DefaultCategoryId, source.MaxImport, maxNew: 1);
        source.LastImportAt = DateTime.Now;
        source.TotalImported += r.added;
        return r.added;   // 0 hoặc 1
    }

    // Đây là luồng xử lý tự đoán danh mục cho bài nhập (miễn phí, không AI)
    // Cách đoán: 1) khớp thẻ <category> của feed với tên danh mục
    //            2) dò tên danh mục xuất hiện trong tiêu đề+tóm tắt. Không khớp -> null (để AI xử lý tiếp).
    // Khớp danh mục có sẵn (MIỄN PHÍ, không gọi AI): ưu tiên thẻ <category> của RSS,
    // sau đó dò tên danh mục xuất hiện trong tiêu đề + tóm tắt. Trả về CategoryId hoặc null.
    private static int? MatchCategory(List<Category> cats, List<string> rssCats, string title, string summary)
    {
        if (cats == null || cats.Count == 0) return null;
        string N(string s) => SlugHelper.MakeSlug(s ?? "");

        // 1) Thẻ <category> trong feed khớp tên danh mục
        foreach (var rc in rssCats)
        {
            var r = N(rc);
            if (r.Length == 0) continue;
            var m = cats.FirstOrDefault(c => N(c.Name) == r)
                 ?? cats.FirstOrDefault(c => { var cn = N(c.Name); return cn.Length >= 3 && (r.Contains(cn) || cn.Contains(r)); });
            if (m != null) return m.Id;
        }

        // 2) Tên danh mục xuất hiện trong tiêu đề + tóm tắt (ưu tiên tên dài/cụ thể hơn)
        var hay = N(title + " " + summary);
        foreach (var c in cats.OrderByDescending(c => c.Name.Length))
        {
            var cn = N(c.Name);
            if (cn.Length >= 3 && hay.Contains(cn)) return c.Id;
        }
        return null;
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
        // [ĐÃ GỠ] Không chèn đoạn "📌 Đây là bài tổng hợp..." nữa — nguồn đã hiển thị ở khu tác giả trên trang chi tiết.
        return sb.ToString();
    }

    // Đây là luồng xử lý LẤY TOÀN VĂN bài báo từ trang gốc (Readability).
    // Cách làm: tải HTML trang bài gốc rồi dùng SmartReader (bản .NET của thuật toán Readability
    // trong Firefox "Reader View") để bóc phần nội dung chính, bỏ menu/quảng cáo/chân trang.
    // Trả (HTML nội dung sạch, văn bản thuần để làm tóm tắt, ảnh đại diện) hoặc null nếu thất bại.
    // Thất bại -> caller giữ nguyên excerpt của feed. Luôn kèm ghi nguồn ở tầng trên (SourceName/SourceUrl).
    private async Task<(string bodyHtml, string plain, string? image)?> TryExtractFullAsync(string url)
    {
        try
        {
            string html;
            try
            {
                // Timeout riêng 12s cho mỗi trang gốc: 1 trang chậm/bị chặn không làm treo cả lượt nhập.
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                html = await _http.GetStringAsync(url, cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không tải được trang gốc để lấy toàn văn: {Url}", url);
                return null;
            }
            if (string.IsNullOrWhiteSpace(html)) return null;

            // Dò VIDEO nhúng trong trang gốc (iframe / .mp4 / .m3u8) để xem ngay trên web mình
            var videoHtml = ExtractVideoHtml(html, url);

            var reader = new SmartReader.Reader(url, html);
            var article = reader.GetArticle();
            var readableOk = article != null && article.IsReadable && !string.IsNullOrWhiteSpace(article.Content);
            var plain = readableOk ? (article!.TextContent ?? "").Trim() : "";

            // Không có video mà chữ lại quá ngắn/không bóc được -> coi như hỏng, dùng excerpt của feed.
            // Có video thì vẫn giữ (bản tin video thường ít chữ).
            if (videoHtml == null && (!readableOk || plain.Length < 400)) return null;

            var cleaned = readableOk ? CleanExtractedHtml(article!.Content) : "";
            if (videoHtml != null) cleaned = videoHtml + cleaned;   // chèn trình phát lên đầu bài
            var image = (readableOk && !string.IsNullOrWhiteSpace(article!.FeaturedImage)) ? article!.FeaturedImage : null;
            return (cleaned, plain, image);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Trích xuất toàn văn thất bại: {Url}", url);
            return null;
        }
    }

    // Đây là luồng xử lý PHÁT HIỆN VIDEO nhúng trong trang bài gốc để phát ngay trên web mình.
    // Ưu tiên: iframe nền tảng phổ biến (YouTube/Vimeo/…) -> file .mp4 (phát trực tiếp) ->
    // luồng HLS .m3u8 (cần hls.js ở trang chi tiết). Không thấy -> null (bài chỉ có chữ).
    // Trả về một khối HTML trình phát để chèn lên đầu nội dung bài.
    private static string? ExtractVideoHtml(string html, string pageUrl)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var rx = System.Text.RegularExpressions.RegexOptions.IgnoreCase;

        // 1) iframe nhúng từ nền tảng video phổ biến
        var ifr = System.Text.RegularExpressions.Regex.Match(html,
            @"<iframe[^>]+src=[""']([^""']*(?:youtube\.com/embed|youtube-nocookie\.com/embed|player\.vimeo\.com|dailymotion\.com/embed|facebook\.com/plugins/video|streamable\.com)[^""']*)[""']", rx);
        if (ifr.Success)
        {
            var src = NormalizeUrl(ifr.Groups[1].Value);
            return $"<div class=\"wi-video-embed wi-video-embed-iframe\"><iframe src=\"{src}\" frameborder=\"0\" allow=\"autoplay; encrypted-media; picture-in-picture\" allowfullscreen></iframe></div>";
        }

        string? mp4 = null, hls = null;

        // 2) JSON-LD VideoObject: "contentUrl": "....mp4|.m3u8"
        var jsonld = System.Text.RegularExpressions.Regex.Match(html,
            @"""contentUrl""\s*:\s*""([^""]+\.(?:mp4|m3u8)[^""]*)""", rx);
        if (jsonld.Success)
        {
            var u = NormalizeUrl(System.Net.WebUtility.HtmlDecode(jsonld.Groups[1].Value));
            if (u.Contains(".m3u8")) hls = u; else mp4 = u;
        }

        // 3) URL .mp4 bất kỳ trong trang (thẻ <source>, data-src, JSON player…)
        if (mp4 == null)
        {
            var m = System.Text.RegularExpressions.Regex.Match(html, @"(?:https?:)?//[^""'\s\\]+\.mp4[^""'\s\\]*", rx);
            if (m.Success) mp4 = NormalizeUrl(m.Value);
        }
        // 4) URL .m3u8 (HLS)
        if (hls == null)
        {
            var m = System.Text.RegularExpressions.Regex.Match(html, @"(?:https?:)?//[^""'\s\\]+\.m3u8[^""'\s\\]*", rx);
            if (m.Success) hls = NormalizeUrl(m.Value);
        }

        if (mp4 != null)
            return $"<div class=\"wi-video-embed\"><video controls playsinline preload=\"metadata\"><source src=\"{mp4}\" type=\"video/mp4\"></video></div>";
        if (hls != null)
            return $"<div class=\"wi-video-embed\"><video class=\"wi-hls\" controls playsinline preload=\"none\" data-hls=\"{hls}\"></video></div>";

        return null;
    }

    // Chuẩn hóa URL: //cdn... -> https://cdn...; giữ nguyên nếu đã đầy đủ.
    private static string NormalizeUrl(string u)
    {
        u = u.Trim();
        if (u.StartsWith("//")) return "https:" + u;
        return u;
    }

    // Làm sạch nhẹ HTML mà SmartReader trả về:
    //  - Gỡ icon SVG (logo YouTube/Facebook/Zalo… của trang gốc) lọt vào nội dung -> tránh phình to hết cỡ.
    //  - Gỡ khối <svg>…</svg> nội tuyến (icon vector) vì bài báo không cần.
    //  - Gỡ thuộc tính sự kiện on* còn sót cho an toàn.
    // (KHÔNG xóa width/height nữa để ảnh nhỏ/icon giữ đúng kích thước; ảnh lớn đã có CSS max-width:100%.)
    private static string CleanExtractedHtml(string html)
    {
        var s = html;
        var opt = System.Text.RegularExpressions.RegexOptions.IgnoreCase;
        s = System.Text.RegularExpressions.Regex.Replace(s, @"<img[^>]+src=[""'][^""']+\.svg[^""']*[""'][^>]*>", "", opt);
        s = System.Text.RegularExpressions.Regex.Replace(s, @"<svg[\s\S]*?</svg>", "", opt);
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\son\w+=""[^""]*""", "", opt);
        return s.Trim();
    }

    // Đây là luồng xử lý lấy ảnh đại diện của tin RSS
    // Thứ tự ưu tiên: enclosure(image) -> media:content -> media:thumbnail -> regex <img src> trong mô tả. Không có -> null.
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

    // Đây là luồng xử lý nhập nhiều bài từ một nguồn RSS
    // Import từ RssSource object
    public async Task<(int added, int updated, int skipped)> ImportFromSourceAsync(RssSource source, string? importedBy = null)
    {
        if (source.SourceType == "video")
        {
            var rv = await ImportVideoRssAsync(source, importedBy: importedBy);
            source.LastImportAt = DateTime.Now;
            source.TotalImported += rv.added;
            return (rv.added, rv.updated, rv.skipped);
        }
        var result = await ImportRssAsync(source.FeedUrl, source.Name, source.DefaultCategoryId, source.MaxImport, importedBy: importedBy);
        source.LastImportAt = DateTime.Now;
        source.TotalImported += result.added;
        return (result.added, result.updated, result.skipped);
    }

    // Đây là luồng xử lý NHẬP VIDEO từ RSS/Atom (vd: feed kênh YouTube).
    // Mỗi mục -> 1 bản ghi Video: lấy YouTube ID (từ <yt:videoId> hoặc từ link),
    // đặt Source = tên nguồn (GHI TÁC nguồn nhập). Trùng YouTubeId -> bỏ qua.
    // Hỗ trợ cả <item> (RSS) lẫn <entry> (Atom/YouTube). Trả (connected, added, updated, skipped).
    public async Task<(bool connected, int added, int updated, int skipped)> ImportVideoRssAsync(
        RssSource source, int maxNew = int.MaxValue, string? importedBy = null)
    {
        int added = 0, skipped = 0;

        string xml;
        try { xml = await _http.GetStringAsync(source.FeedUrl); }
        catch (Exception ex) { _logger.LogWarning(ex, "Tải RSS video thất bại: {Url}", source.FeedUrl); return (false, 0, 0, 0); }

        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch (Exception ex) { _logger.LogWarning(ex, "Parse RSS video thất bại: {Url}", source.FeedUrl); return (false, 0, 0, 0); }

        XNamespace atom = "http://www.w3.org/2005/Atom";
        XNamespace yt = "http://www.youtube.com/xml/schemas/2015";
        XNamespace media = "http://search.yahoo.com/mrss/";

        // Gộp cả <item> (RSS thường) và <entry> (Atom — feed YouTube dùng dạng này)
        var nodes = doc.Descendants("item")
            .Concat(doc.Descendants(atom + "entry"))
            .Take(source.MaxImport > 0 ? source.MaxImport : 30);

        foreach (var it in nodes)
        {
            // 1) YouTube ID: ưu tiên <yt:videoId>, không có thì bóc từ link
            string? ytId = (string?)it.Element(yt + "videoId");
            if (string.IsNullOrWhiteSpace(ytId))
            {
                var linkVal = (string?)it.Element("link");
                if (string.IsNullOrWhiteSpace(linkVal))
                {
                    var linkEl = it.Elements(atom + "link").FirstOrDefault() ?? it.Elements("link").FirstOrDefault();
                    if (linkEl != null) linkVal = linkEl.Attribute("href")?.Value ?? linkEl.Value;
                }
                ytId = YouTubeHelper.ExtractId(linkVal);
            }
            if (string.IsNullOrWhiteSpace(ytId)) continue;   // không phải video YouTube -> bỏ

            // Trùng -> bỏ qua
            if (await _db.Videos.AnyAsync(v => v.YouTubeId == ytId)) { skipped++; continue; }

            var title = System.Net.WebUtility.HtmlDecode(
                ((string?)it.Element("title") ?? (string?)it.Element(atom + "title") ?? "").Trim());
            if (string.IsNullOrWhiteSpace(title)) title = "Video";

            // Mô tả: description (RSS) / summary (Atom) / media:group>media:description
            var mediaGroup = it.Element(media + "group");
            string? desc = (string?)it.Element("description")
                ?? (string?)it.Element(atom + "summary")
                ?? (mediaGroup != null ? (string?)mediaGroup.Element(media + "description") : null);
            desc = string.IsNullOrWhiteSpace(desc) ? null : System.Net.WebUtility.HtmlDecode(desc).Trim();
            if (desc != null && desc.Length > 1000) desc = desc.Substring(0, 1000).TrimEnd() + "…";

            // Ngày đăng
            var published = DateTime.Now;
            var pub = (string?)it.Element("pubDate")
                ?? (string?)it.Element(atom + "published")
                ?? (string?)it.Element(atom + "updated");
            if (!string.IsNullOrWhiteSpace(pub) &&
                DateTimeOffset.TryParse(pub, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dto))
                published = dto.LocalDateTime;

            _db.Videos.Add(new Video
            {
                Title = title,
                YouTubeId = ytId,
                Source = source.Name,      // GHI TÁC: nguồn nhập
                Description = desc,
                VideoType = "youtube",
                Status = "published",
                PublishedAt = published,
                CreatedAt = DateTime.Now
            });
            added++;
            if (added >= maxNew) break;
        }

        if (added > 0) await _db.SaveChangesAsync();
        return (true, added, 0, skipped);
    }
}
