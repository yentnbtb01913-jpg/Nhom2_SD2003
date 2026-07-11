using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Services;

/// <summary>
/// Logic dùng CHUNG cho trang "Kênh Bên Ngoài" (bài IsExternal = true) ở cả khu Admin và Nhân viên.
/// Gồm: danh sách/lọc bài nhập tự động, quản lý avatar nguồn, và thuật toán cộng/sửa/hoàn tác
/// lượt xem mẫu theo delta (SeedViewBatch) — sửa/xóa CHỈ tác động cột Views, không đụng bài/bình luận.
/// </summary>
public class ExternalArticleService
{
    private readonly AppDbContext _db;
    public ExternalArticleService(AppDbContext db) { _db = db; }

    private const int PageSize = 20;

    // ===== DANH SÁCH BÀI (Kênh Bên Ngoài) =====

    public async Task<(List<Article> Items, int Total, int TotalPages)> GetListAsync(string? sourceName, int page)
    {
        if (page < 1) page = 1;
        var query = _db.Articles.Include(a => a.Category).Where(a => a.IsExternal == true);

        if (!string.IsNullOrWhiteSpace(sourceName))
            query = query.Where(a => a.SourceName == sourceName);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        return (items, total, totalPages);
    }

    public Task<List<RssSource>> GetSourcesAsync() => _db.RssSources.OrderBy(s => s.Name).ToListAsync();

    /// <summary>
    /// Danh sách bài (Kênh Bên Ngoài) dùng cho ô "Theo bài báo cụ thể" trong modal Tạo lượt xem mẫu:
    /// preview (ảnh/tiêu đề/nguồn/danh mục) + lượt xem hiện tại để sửa trực tiếp.
    /// Include Category để tránh N+1; giới hạn số bản ghi để danh sách gọn.
    /// keyword: lọc theo tiêu đề hoặc tên nguồn. sourceName: lọc đúng theo nguồn nhập (bộ lọc "Nguồn nhập"), null/rỗng = tất cả nguồn.
    /// </summary>
    public async Task<List<Article>> SearchForSeedPickerAsync(string? keyword, string? sourceName = null, int take = 200)
    {
        var query = _db.Articles.AsNoTracking().Include(a => a.Category).Where(a => a.IsExternal == true);

        if (!string.IsNullOrWhiteSpace(sourceName))
            query = query.Where(a => a.SourceName == sourceName);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(a => a.Title.Contains(kw) || (a.SourceName != null && a.SourceName.Contains(kw)));
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    /// <summary>Đặt lại Views của một bài (Kênh Bên Ngoài) thành đúng con số nhập vào (không cho âm). Dùng cho sửa trực tiếp trong picker.</summary>
    public async Task<(bool Success, string Message, int Views)> SetArticleViewsAsync(int articleId, int views)
    {
        var art = await _db.Articles.FindAsync(articleId);
        if (art == null || !art.IsExternal)
            return (false, "Không tìm thấy bài viết (thuộc Kênh Bên Ngoài) với ID này", 0);

        art.Views = Math.Max(0, views);
        await _db.SaveChangesAsync();
        return (true, "Đã cập nhật lượt xem", art.Views);
    }

    /// <summary>Ảnh đại diện nguồn: LogoUrl thủ công &gt; favicon Google theo domain &gt; ảnh mặc định (client tự onerror fallback).</summary>
    public static string ResolveAvatarUrl(RssSource? source)
    {
        if (source != null && !string.IsNullOrWhiteSpace(source.LogoUrl))
            return source.LogoUrl.Trim();

        var host = ExtractHost(source?.WebsiteUrl) ?? ExtractHost(source?.FeedUrl);
        if (!string.IsNullOrEmpty(host))
            return $"https://www.google.com/s2/favicons?domain={host}&sz=64";

        return "/images/default-avatar.png";
    }

    private static string? ExtractHost(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            var u = url.Contains("://") ? url : "https://" + url;
            return new Uri(u).Host;
        }
        catch { return null; }
    }

    // ===== QUẢN LÝ AVATAR NGUỒN =====

    public async Task<(bool Success, string Message)> SaveSourceLogoAsync(int sourceId, string? logoUrl)
    {
        var src = await _db.RssSources.FindAsync(sourceId);
        if (src == null) return (false, "Không tìm thấy nguồn tin");
        src.LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim();
        await _db.SaveChangesAsync();
        return (true, "Đã lưu avatar nguồn");
    }

    // ===== TẠO LƯỢT XEM MẪU (theo phạm vi) =====

    private static long RollDelta(int min, int max)
    {
        if (min > max) (min, max) = (max, min);
        if (min < 0) min = 0;
        return min == max ? min : Random.Shared.Next(min, max + 1);
    }

    public async Task<(bool Success, string Message, SeedViewBatch? Batch)> CreateSeedBatchAsync(
        string scope, int? articleId, int? sourceId, int? categoryId,
        int minViews, int maxViews, DateTime? fromDate, DateTime? toDate, string editorName)
    {
        if (minViews < 0 || maxViews < 0 || minViews > maxViews)
            return (false, "Khoảng lượt xem không hợp lệ (tối thiểu phải ≤ tối đa và ≥ 0)", null);

        var query = _db.Articles.Where(a => a.IsExternal == true);
        string targetLabel;

        switch (scope)
        {
            case "article":
                if (articleId == null) return (false, "Thiếu ID bài viết", null);
                query = query.Where(a => a.Id == articleId.Value);
                var art = await _db.Articles.FindAsync(articleId.Value);
                if (art == null || !art.IsExternal) return (false, "Không tìm thấy bài viết (thuộc Kênh Bên Ngoài) với ID này", null);
                targetLabel = $"Bài #{art.Id}: {art.Title}";
                break;
            case "source":
                if (sourceId == null) return (false, "Thiếu nguồn tin", null);
                var src = await _db.RssSources.FindAsync(sourceId.Value);
                if (src == null) return (false, "Không tìm thấy nguồn tin", null);
                query = query.Where(a => a.SourceName == src.Name);
                targetLabel = src.Name;
                break;
            case "category":
                if (categoryId == null) return (false, "Thiếu danh mục", null);
                var cat = await _db.Categories.FindAsync(categoryId.Value);
                if (cat == null) return (false, "Không tìm thấy danh mục", null);
                query = query.Where(a => a.CategoryId == categoryId.Value);
                targetLabel = cat.Name;
                break;
            case "all":
                targetLabel = "Tất cả bài viết (Kênh Bên Ngoài)";
                break;
            default:
                return (false, "Phạm vi không hợp lệ", null);
        }

        if (fromDate.HasValue) query = query.Where(a => a.PublishedAt >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(a => a.PublishedAt < toDate.Value.AddDays(1));

        var articles = await query.ToListAsync();
        if (articles.Count == 0) return (false, "Không có bài viết nào khớp phạm vi đã chọn", null);

        var details = new Dictionary<string, long>();
        long totalAdded = 0;
        foreach (var a in articles)
        {
            var delta = RollDelta(minViews, maxViews);
            if (delta <= 0) continue;
            a.Views += (int)delta;
            details[a.Id.ToString()] = delta;
            totalAdded += delta;
        }

        var batch = new SeedViewBatch
        {
            Scope = scope,
            TargetLabel = targetLabel,
            ArticleCount = details.Count,
            MinViews = minViews,
            MaxViews = maxViews,
            TotalAdded = totalAdded,
            DetailsJson = JsonSerializer.Serialize(details),
            EditorName = string.IsNullOrWhiteSpace(editorName) ? "Hệ thống" : editorName,
            CreatedAt = DateTime.Now
        };
        _db.SeedViewBatches.Add(batch);
        await _db.SaveChangesAsync();

        return (true, $"Đã cộng {totalAdded:N0} lượt xem cho {details.Count} bài viết", batch);
    }

    // ===== QUẢN LÝ / SỬA / XÓA (HOÀN TÁC) LƯỢT XEM MẪU =====

    public Task<List<SeedViewBatch>> GetBatchesAsync() => _db.SeedViewBatches.OrderByDescending(b => b.CreatedAt).ToListAsync();

    private static Dictionary<int, long> ParseDetails(string json)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, long>>(json) ?? new();
        var result = new Dictionary<int, long>();
        foreach (var kv in raw)
            if (int.TryParse(kv.Key, out var id)) result[id] = kv.Value;
        return result;
    }

    /// <summary>Sửa khoảng min–max: gỡ lượt xem cũ (trừ đúng delta đã lưu) rồi cộng lại theo khoảng mới trên ĐÚNG các bài đó.</summary>
    public async Task<(bool Success, string Message)> EditSeedBatchAsync(int batchId, int minViews, int maxViews)
    {
        if (minViews < 0 || maxViews < 0 || minViews > maxViews)
            return (false, "Khoảng lượt xem không hợp lệ");

        var batch = await _db.SeedViewBatches.FindAsync(batchId);
        if (batch == null) return (false, "Không tìm thấy đợt lượt xem mẫu này");

        var oldDeltas = ParseDetails(batch.DetailsJson);
        if (oldDeltas.Count == 0) return (false, "Đợt này không có bài viết nào để sửa");

        var ids = oldDeltas.Keys.ToList();
        var articles = await _db.Articles.Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id);

        var newDeltas = new Dictionary<string, long>();
        long totalAdded = 0;
        foreach (var id in ids)
        {
            if (!articles.TryGetValue(id, out var a)) continue; // bài đã bị xóa từ nơi khác — bỏ qua an toàn

            // Gỡ delta cũ (clamp không âm)
            a.Views = Math.Max(0, a.Views - (int)oldDeltas[id]);

            // Cộng lại theo khoảng mới
            var newDelta = RollDelta(minViews, maxViews);
            a.Views += (int)newDelta;
            newDeltas[id.ToString()] = newDelta;
            totalAdded += newDelta;
        }

        batch.MinViews = minViews;
        batch.MaxViews = maxViews;
        batch.TotalAdded = totalAdded;
        batch.ArticleCount = newDeltas.Count;
        batch.DetailsJson = JsonSerializer.Serialize(newDeltas);

        await _db.SaveChangesAsync();
        return (true, $"Đã cập nhật đợt: {totalAdded:N0} lượt xem trên {newDeltas.Count} bài");
    }

    /// <summary>Xóa đợt: TRỪ đúng số đã cộng theo DetailsJson (clamp không âm) rồi xóa batch. Không đụng bài viết/bình luận.</summary>
    public async Task<(bool Success, string Message)> DeleteSeedBatchAsync(int batchId)
    {
        var batch = await _db.SeedViewBatches.FindAsync(batchId);
        if (batch == null) return (false, "Không tìm thấy đợt lượt xem mẫu này");

        var deltas = ParseDetails(batch.DetailsJson);
        if (deltas.Count > 0)
        {
            var ids = deltas.Keys.ToList();
            var articles = await _db.Articles.Where(a => ids.Contains(a.Id)).ToListAsync();
            foreach (var a in articles)
                if (deltas.TryGetValue(a.Id, out var delta))
                    a.Views = Math.Max(0, a.Views - (int)delta);
        }

        _db.SeedViewBatches.Remove(batch);
        await _db.SaveChangesAsync();
        return (true, "Đã hoàn tác và xóa đợt lượt xem mẫu");
    }
}
