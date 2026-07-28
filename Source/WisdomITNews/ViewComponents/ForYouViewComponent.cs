using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.ViewComponents;

// "Dành cho bạn" — gợi ý ở trang chi tiết.
//  * Đăng nhập & có sở thích: gợi ý theo danh mục THEO DÕI (UserCategoryFollow) + danh mục bài ĐÃ LƯU (SavedArticle).
//  * Guest hoặc chưa có sở thích: hiện bài MỚI NHẬP VỀ & ÍT VIEW (để lộ tin mới, chưa nhiều người xem).
// Luôn hiển thị (miễn có bài), nên khách vãng lai cũng thấy mục này.
public class ForYouViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    public ForYouViewComponent(AppDbContext db) { _db = db; }

    // Chiếu "thẻ" nhẹ, bỏ cột Content nặng.
    private static readonly Expression<Func<Article, Article>> Card = a => new Article
    {
        Id = a.Id,
        Title = a.Title,
        Slug = a.Slug,
        Thumbnail = a.Thumbnail,
        Summary = a.Summary,
        PublishedAt = a.PublishedAt,
        CreatedAt = a.CreatedAt,
        Views = a.Views,
        CategoryId = a.CategoryId,
        Category = a.Category == null ? null : new Category
        {
            Id = a.Category.Id,
            Name = a.Category.Name,
            Slug = a.Category.Slug,
            Icon = a.Category.Icon
        }
    };

    public async Task<IViewComponentResult> InvokeAsync(int excludeArticleId, int take = 8)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        List<Article> arts = new();
        bool personalized = false;

        // 1) Cá nhân hóa (chỉ khi đăng nhập và có sở thích)
        if (userId != null)
        {
            var followedCatIds = await _db.UserCategoryFollows
                .Where(f => f.UserId == userId.Value)
                .Select(f => f.CategoryId).ToListAsync();

            var savedArticleIds = await _db.SavedArticles
                .Where(s => s.UserId == userId.Value)
                .Select(s => s.ArticleId).ToListAsync();

            var savedCatIds = await _db.Articles
                .Where(a => savedArticleIds.Contains(a.Id) && a.CategoryId != null)
                .Select(a => a.CategoryId!.Value).Distinct().ToListAsync();

            var catIds = followedCatIds.Concat(savedCatIds).Distinct().ToList();
            if (catIds.Count > 0)
            {
                arts = await _db.Articles
                    .Where(a => a.Status == "published" && a.CategoryId != null
                                && catIds.Contains(a.CategoryId.Value)
                                && a.Id != excludeArticleId
                                && !savedArticleIds.Contains(a.Id))
                    .OrderByDescending(a => a.PublishedAt)
                    .Select(Card)
                    .Take(take)
                    .ToListAsync();
                personalized = arts.Count > 0;
            }
        }

        // 2) Guest / chưa có sở thích: bài MỚI NHẬP VỀ & ÍT VIEW
        if (arts.Count == 0)
        {
            // Lấy pool 60 bài mới nhất rồi ưu tiên bài ít view nhất
            var pool = await _db.Articles
                .Where(a => a.Status == "published" && a.Id != excludeArticleId)
                .OrderByDescending(a => a.CreatedAt)
                .Take(60)
                .Select(Card)
                .ToListAsync();

            arts = pool.OrderBy(a => a.Views).ThenByDescending(a => a.CreatedAt).Take(take).ToList();
        }

        if (arts.Count == 0) return Content(string.Empty);
        ViewBag.ForYouPersonalized = personalized;
        return View(arts);
    }
}
