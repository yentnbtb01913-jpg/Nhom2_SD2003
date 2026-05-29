using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.ViewComponents;

/// <summary>
/// [C] Mega menu: render danh sách category gốc + Children dropdown.
/// Sử dụng trong _Layout.cshtml: @await Component.InvokeAsync("CategoryMenu")
/// </summary>
public class CategoryMenuViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    private readonly ILogger<CategoryMenuViewComponent> _logger;

    public CategoryMenuViewComponent(AppDbContext db, ILogger<CategoryMenuViewComponent> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        try
        {
            var roots = await _db.Categories
                .Where(c => c.IsVisible && c.ParentCategoryId == null)
                .OrderBy(c => c.SortOrder)
                .Include(c => c.Children.Where(ch => ch.IsVisible).OrderBy(ch => ch.SortOrder))
                .AsNoTracking()
                .ToListAsync();

            return View(roots);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CategoryMenu failed to load — fallback empty list");
            return View(new List<Category>());
        }
    }
}
