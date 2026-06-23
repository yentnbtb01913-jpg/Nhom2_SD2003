using Microsoft.AspNetCore.Mvc;
using WisdomITNews.Services;

namespace WisdomITNews.ViewComponents;

/// <summary>
/// [I] Weather widget — đọc vùng từ session, gọi WeatherService và render header weather.
/// </summary>
public class WeatherViewComponent : ViewComponent
{
    private readonly WeatherService _weather;

    // Map region slug → tên thành phố cho OpenWeatherMap
    private static readonly Dictionary<string, string> RegionWeatherMap = new()
    {
        ["dong-nai"] = "Bien Hoa",
        ["ha-noi"] = "Hanoi",
        ["ho-chi-minh"] = "Ho Chi Minh",
        ["da-nang"] = "Da Nang",
        ["hai-phong"] = "Hai Phong",
        ["can-tho"] = "Can Tho",
    };

    public WeatherViewComponent(WeatherService weather)
    {
        _weather = weather;
    }

    public async Task<IViewComponentResult> InvokeAsync(string? city = null)
    {
        // Ưu tiên: tham số truyền vào → session → mặc định "Bien Hoa"
        if (string.IsNullOrWhiteSpace(city))
        {
            var regionSlug = HttpContext.Session.GetString("CurrentRegion") ?? "dong-nai";
            city = RegionWeatherMap.GetValueOrDefault(regionSlug, "Bien Hoa");
        }

        var data = await _weather.GetWeatherAsync(city);
        return View(data);
    }
}