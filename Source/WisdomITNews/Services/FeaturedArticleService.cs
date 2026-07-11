using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Services;

/// <summary>
/// Logic dùng CHUNG cho khu "Tin nổi bật tự động" — cả Trang chủ (khu vực lớn) và tab quản lý
/// "Bài viết nổi bật" đều gọi qua service này để đảm bảo cùng một cách tính điểm/xếp hạng.
/// Điểm/thứ hạng luôn tính LÚC ĐỌC (không lưu vị trí cố định) nên tự đồng bộ mỗi khi Views đổi.
/// </summary>
public class FeaturedArticleService
{
    private readonly AppDbContext _db;
    public FeaturedArticleService(AppDbContext db) { _db = db; }

    /// <summary>Điều kiện tham gia xếp hạng (mục III): published, không bị loại, có ảnh, có danh mục,
    /// có lượt xem &gt; 0, có nguồn tin hoặc tác giả hợp lệ.</summary>
    public IQueryable<Article> EligibleQuery()
    {
        return _db.Articles.AsNoTracking()
            .Where(a => a.Status == "published"
                && !a.FeaturedHidden
                && !string.IsNullOrEmpty(a.Thumbnail)
                && a.CategoryId != null
                && a.Views > 0
                && (a.AuthorId != null || a.AuthorUserId != null || !string.IsNullOrEmpty(a.SourceName)));
    }

    /// <summary>Sắp xếp theo mục IV: Ghim &gt; Lượt xem &gt; Ngày xuất bản &gt; Ngày cập nhật.</summary>
    public IQueryable<Article> RankedQuery()
    {
        return EligibleQuery()
            .Include(a => a.Category)
            .OrderByDescending(a => a.FeaturedPinned)
            .ThenByDescending(a => a.Views)
            .ThenByDescending(a => a.PublishedAt)
            .ThenByDescending(a => a.UpdatedAt);
    }

    /// <summary>Trang chủ: TOP 4 (mục VI) — [0] = bài chính, [1..3] = 3 bài phụ.
    /// Nếu chưa đủ 4 bài đủ điều kiện thì bù thêm bài mới xuất bản gần nhất (không trùng).</summary>
    public async Task<List<Article>> GetHomepageTop4Async()
    {
        var top = await RankedQuery().Take(4).ToListAsync();
        if (top.Count < 4)
        {
            var existIds = top.Select(a => a.Id).ToHashSet();
            var fillers = await _db.Articles.AsNoTracking()
                .Include(a => a.Category)
                .Where(a => a.Status == "published" && !existIds.Contains(a.Id))
                .OrderByDescending(a => a.PublishedAt)
                .Take(4 - top.Count)
                .ToListAsync();
            top.AddRange(fillers);
        }
        return top;
    }

    /// <summary>Tab quản lý: bảng xếp hạng hiện tại (mặc định top 50).</summary>
    public Task<List<Article>> GetRankingListAsync(int take = 50) => RankedQuery().Take(take).ToListAsync();

    /// <summary>Khu "Bài đã loại" — các bài đang bật FeaturedHidden.</summary>
    public Task<List<Article>> GetHiddenListAsync() =>
        _db.Articles.AsNoTracking().Include(a => a.Category)
            .Where(a => a.FeaturedHidden)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync();

    // ===== Ghim / Loại — loại trừ lẫn nhau (mục VII) =====

    public async Task<(bool Success, string Message)> PinAsync(int articleId)
    {
        var a = await _db.Articles.FindAsync(articleId);
        if (a == null) return (false, "Không tìm thấy bài viết");
        a.FeaturedPinned = true;
        a.FeaturedHidden = false;   // ghim thì tự bỏ loại
        a.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return (true, "Đã ghim bài viết lên đầu khu nổi bật");
    }

    public async Task<(bool Success, string Message)> UnpinAsync(int articleId)
    {
        var a = await _db.Articles.FindAsync(articleId);
        if (a == null) return (false, "Không tìm thấy bài viết");
        a.FeaturedPinned = false;
        await _db.SaveChangesAsync();
        return (true, "Đã bỏ ghim — bài trở lại xếp hạng tự động");
    }

    public async Task<(bool Success, string Message)> HideAsync(int articleId)
    {
        var a = await _db.Articles.FindAsync(articleId);
        if (a == null) return (false, "Không tìm thấy bài viết");
        a.FeaturedHidden = true;
        a.FeaturedPinned = false;   // loại thì tự bỏ ghim
        await _db.SaveChangesAsync();
        return (true, "Đã loại khỏi khu nổi bật (bài viết, lượt xem, bình luận vẫn giữ nguyên)");
    }

    public async Task<(bool Success, string Message)> UnhideAsync(int articleId)
    {
        var a = await _db.Articles.FindAsync(articleId);
        if (a == null) return (false, "Không tìm thấy bài viết");
        a.FeaturedHidden = false;
        await _db.SaveChangesAsync();
        return (true, "Đã đưa bài trở lại xét duyệt khu nổi bật");
    }
}
