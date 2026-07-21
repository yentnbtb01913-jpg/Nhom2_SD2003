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

    // Đây là luồng xử lý lọc bài đủ điều kiện Tin nổi bật tự động
    // Luồng lọc: Status="published" AND !FeaturedHidden AND có Thumbnail AND có CategoryId
    //            AND Views>0 AND (có AuthorId/AuthorUserId HOẶC có SourceName).
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

    // Đây là luồng xử lý xếp hạng Tin nổi bật tự động
    // Thứ tự ưu tiên: FeaturedPinned (ghim) > Views > PublishedAt > UpdatedAt (giảm dần).
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

    // Đây là luồng xử lý lấy Top 4 Tin nổi bật cho trang chủ
    // Luồng: 1) Lấy 4 bài đầu theo RankedQuery
    //        2) Nếu chưa đủ 4 -> bù bằng bài "published" mới nhất chưa có trong danh sách
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

    // Đây là luồng xử lý bảng xếp hạng Tin nổi bật (tab quản lý)
    /// <summary>Tab quản lý: bảng xếp hạng hiện tại (mặc định top 50).</summary>
    public Task<List<Article>> GetRankingListAsync(int take = 50) => RankedQuery().Take(take).ToListAsync();

    // Đây là luồng xử lý danh sách bài đã loại khỏi Tin nổi bật
    /// <summary>Khu "Bài đã loại" — các bài đang bật FeaturedHidden.</summary>
    public Task<List<Article>> GetHiddenListAsync() =>
        _db.Articles.AsNoTracking().Include(a => a.Category)
            .Where(a => a.FeaturedHidden)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync();

    // ===== Ghim / Loại — loại trừ lẫn nhau (mục VII) =====

    // Đây là luồng xử lý ghim bài Tin nổi bật
    // Bảng: Articles
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

    // Đây là luồng xử lý bỏ ghim bài Tin nổi bật
    public async Task<(bool Success, string Message)> UnpinAsync(int articleId)
    {
        var a = await _db.Articles.FindAsync(articleId);
        if (a == null) return (false, "Không tìm thấy bài viết");
        a.FeaturedPinned = false;
        await _db.SaveChangesAsync();
        return (true, "Đã bỏ ghim — bài trở lại xếp hạng tự động");
    }

    // Đây là luồng xử lý loại bài khỏi Tin nổi bật
    // Bảng: Articles
    public async Task<(bool Success, string Message)> HideAsync(int articleId)
    {
        var a = await _db.Articles.FindAsync(articleId);
        if (a == null) return (false, "Không tìm thấy bài viết");
        a.FeaturedHidden = true;
        a.FeaturedPinned = false;   // loại thì tự bỏ ghim
        await _db.SaveChangesAsync();
        return (true, "Đã loại khỏi khu nổi bật (bài viết, lượt xem, bình luận vẫn giữ nguyên)");
    }

    // Đây là luồng xử lý bỏ loại bài Tin nổi bật
    public async Task<(bool Success, string Message)> UnhideAsync(int articleId)
    {
        var a = await _db.Articles.FindAsync(articleId);
        if (a == null) return (false, "Không tìm thấy bài viết");
        a.FeaturedHidden = false;
        await _db.SaveChangesAsync();
        return (true, "Đã đưa bài trở lại xét duyệt khu nổi bật");
    }
}
