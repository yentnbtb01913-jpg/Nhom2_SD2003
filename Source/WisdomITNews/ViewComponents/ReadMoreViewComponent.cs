using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.ViewComponents;

// "Đọc thêm" — khối mini trang chủ ở cuối trang bài (lấp khoảng trống khi bài ngắn/video).
// Mỗi chuyên mục hiển thị: 1 bài lead (ảnh) + vài tiêu đề mới nhất. Xếp thành lưới nhiều cột trải xuống.
public class ReadMoreViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    public ReadMoreViewComponent(AppDbContext db) { _db = db; }

    private static readonly Expression<Func<Article, Article>> Card = a => new Article
    {
        Id = a.Id,
        Title = a.Title,
        Slug = a.Slug,
        Thumbnail = a.Thumbnail,
        Summary = a.Summary,
        PublishedAt = a.PublishedAt
    };

    public async Task<IViewComponentResult> InvokeAsync(int? excludeArticleId = null, int perCategory = 5)
    {
        var cats = await _db.Categories
            .Where(c => c.IsVisible && c.ParentCategoryId == null)
            .OrderBy(c => c.SortOrder)
            .AsNoTracking()
            .ToListAsync();

        var blocks = new List<HomeCategoryBlock>();
        foreach (var c in cats)
        {
            var arts = await _db.Articles
                .Where(a => a.Status == "published" && a.CategoryId == c.Id
                            && (excludeArticleId == null || a.Id != excludeArticleId))
                .OrderByDescending(a => a.PublishedAt)
                .Select(Card)
                .Take(perCategory)
                .ToListAsync();

            if (arts.Count > 0)
                blocks.Add(new HomeCategoryBlock { Category = c, Articles = arts });
        }

        return View(blocks);
    }
}
