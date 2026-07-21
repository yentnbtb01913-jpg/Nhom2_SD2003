using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using WisdomITNews.Models;

namespace WisdomITNews.Services;

/// <summary>
/// [I] Gọi OpenWeatherMap, cache 2 phút theo city (gần thời gian thực).
/// </summary>
public class WeatherService
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(
        IHttpClientFactory http,
        IConfiguration config,
        IMemoryCache cache,
        ILogger<WeatherService> logger)
    {
        _http = http;
        _config = config;
        _cache = cache;
        _logger = logger;
    }

    // Đây là luồng xử lý lấy thời tiết theo thành phố (widget)
    // Luồng: 1) Có cache (2 phút) -> trả cache
    //        2) Chưa có API key -> bỏ qua (null)
    //        3) Gọi OpenWeatherMap (timeout 5s) -> parse nhiệt độ/mô tả/icon -> lưu cache 2 phút
    // Lỗi/không có -> trả null (widget tự ẩn).
    public async Task<WeatherViewModel?> GetWeatherAsync(string city)
    {
        if (string.IsNullOrWhiteSpace(city)) return null;

        var key = $"weather_{city.ToLowerInvariant()}";
        if (_cache.TryGetValue<WeatherViewModel>(key, out var cached) && cached != null)
            return cached;

        var apiKey = _config["OpenWeather:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "REPLACE_ME")
        {
            _logger.LogWarning("OpenWeather:ApiKey chưa cấu hình — bỏ qua widget");
            return null;
        }

        try
        {
            var url = $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(city)}&appid={apiKey}&units=metric&lang=vi";
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenWeather trả status {Status} cho city={City}", response.StatusCode, city);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);

            var vm = new WeatherViewModel
            {
                City        = json["name"]?.ToString() ?? city,
                Temp        = json["main"]?["temp"]?.Value<double>() ?? 0,
                Description = json["weather"]?[0]?["description"]?.ToString() ?? "",
                IconCode    = json["weather"]?[0]?["icon"]?.ToString() ?? "01d"
            };

            _cache.Set(key, vm, TimeSpan.FromMinutes(2));
            return vm;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetWeatherAsync({City}) failed", city);
            return null;
        }
    }
}
