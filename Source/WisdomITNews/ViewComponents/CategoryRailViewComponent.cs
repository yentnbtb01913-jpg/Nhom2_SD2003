using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.ViewComponents;

// Cột danh mục ở sidebar (thay cho ô Video): liệt kê TẤT CẢ danh mục hiển thị,
// mỗi danh mục kèm vài bài mới nhất, trải dọc xuống. Dùng projection để không nạp cột Content nặng.
public class CategoryRailViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    public CategoryRailViewComponent(AppDbContext db) { _db = db; }

    public async Task<IViewComponentResult> InvokeAsync(int? excludeArticleId = null, int perCategory = 4)
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
                .Select(a => new Article
                {
                    Id = a.Id,
                    Title = a.Title,
                    Slug = a.Slug,
                    Thumbnail = a.Thumbnail,
                    PublishedAt = a.PublishedAt
                })
                .Take(perCategory)
                .ToListAsync();

            if (arts.Count > 0)
                blocks.Add(new HomeCategoryBlock { Category = c, Articles = arts });
        }

        return View(blocks);
    }
}
