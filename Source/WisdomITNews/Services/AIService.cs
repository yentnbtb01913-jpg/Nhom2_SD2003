using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Services;

public class AIService
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly ILogger<AIService> _logger;
    private readonly IOptions<AiOptions> _options;

    // Giới hạn ký tự content gửi cho Gemini khi tóm tắt bài báo.
    private const int SummarizeContentLimit = 3000;
    // Giới hạn ký tự content dùng để gợi ý tiêu đề.
    private const int SuggestTitleContentLimit = 2000;
    // Giới hạn ký tự content dùng để kiểm duyệt nội dung.
    private const int ModerationContentLimit = 4000;
    private const string DefaultGeminiModel = "models/gemini-2.5-flash";

    public AIService(
        IHttpClientFactory http,
        IConfiguration config,
        AppDbContext db,
        ILogger<AIService> logger,
        IOptions<AiOptions> options)
    {
        _http = http;
        _config = config;
        _db = db;
        _logger = logger;
        _options = options;
    }

    // Cấu hình hiệu lực: dòng DB (chỉnh sống ở trang Quản lý AI) đè lên appsettings.
    // Chưa có dòng DB -> lấy từ appsettings; field trống -> lấy prompt mặc định.
    private async Task<AiSetting> GetEffectiveAsync()
    {
        var row = await _db.AiSettings.AsNoTracking().FirstOrDefaultAsync();
        if (row != null) return row;

        var o = _options.Value;
        static string Or(string? v, string def) => string.IsNullOrWhiteSpace(v) ? def : v;
        return new AiSetting
        {
            Model                = Or(o.Model, DefaultGeminiModel),
            ApiVersion           = Or(o.ApiVersion, "v1beta"),
            Temperature          = o.Temperature,
            MaxOutputTokens      = o.MaxOutputTokens > 0 ? o.MaxOutputTokens : 2048,
            ThinkingBudget       = o.ThinkingBudget,
            SystemInstruction    = o.SystemInstruction ?? "",
            SummarizeLength      = Or(o.Summarize.Length, "150-200 từ"),
            SummarizeTemplate    = Or(o.Summarize.Template, AiDefaults.Summarize),
            SuggestTitleTemplate = Or(o.SuggestTitle.Template, AiDefaults.SuggestTitle),
            ModerateTemplate     = Or(o.Moderate.Template, AiDefaults.Moderate),
            ChatMaxSentences     = o.Chat.MaxSentences > 0 ? o.Chat.MaxSentences : 4,
            ChatTemplate         = Or(o.Chat.Template, AiDefaults.Chat)
        };
    }

    // Đây là luồng xử lý tóm tắt bài viết bằng AI
    // Luồng: 1) Đã có AiSummary -> trả cache
    //        2) Lấy cấu hình hiệu lực (DB đè appsettings), cắt Content ≤3000 ký tự, ghép prompt
    //        3) Gọi Gemini -> lưu AiSummary + ghi AILog (thành công/thất bại)
    public async Task<SummarizeResponse> SummarizeAsync(int articleId)
    {
        var article = await _db.Articles.FindAsync(articleId);
        if (article == null) return new SummarizeResponse { Success = false };
        if (!string.IsNullOrEmpty(article.AiSummary))
            return new SummarizeResponse { Success = true, Summary = article.AiSummary, Cached = true };

        var cfg = await GetEffectiveAsync();
        var content = TrimContent(article.Content, SummarizeContentLimit);
        var prompt = cfg.SummarizeTemplate
            .Replace("{Length}", cfg.SummarizeLength)
            .Replace("{Title}", article.Title)
            .Replace("{Content}", content);

        var (text, tokens, model, error) = await CallModelAsync(prompt, cfg);
        if (error != null || string.IsNullOrWhiteSpace(text))
        {
            await TryLogAsync(new AILog
            {
                ArticleId  = articleId,
                Action     = "summarize",
                PromptText = prompt,
                IsSuccess  = false,
                ErrorMsg   = error ?? "empty response",
                ModelUsed  = model
            }, "AILog save failed (summarize error)");
            return new SummarizeResponse { Success = false };
        }

        article.AiSummary = text;
        article.UpdatedAt = DateTime.Now;
        try
        {
            _db.AILogs.Add(new AILog
            {
                ArticleId  = articleId,
                Action     = "summarize",
                ResultText = text,
                ModelUsed  = model,
                TokensUsed = tokens,
                IsSuccess  = true
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Save failed after successful summarize (articleId={ArticleId})", articleId);
        }
        return new SummarizeResponse { Success = true, Summary = text!, Tokens = tokens };
    }

    // Đây là luồng xử lý gợi ý tiêu đề bài viết bằng AI
    // Luồng: lấy cấu hình + cắt nội dung -> gọi Gemini -> parse JSON 5 tiêu đề (5 phong cách) -> ghi AILog
    public async Task<SuggestTitleResponse> SuggestTitlesAsync(int articleId)
    {
        var article = await _db.Articles.FindAsync(articleId);
        if (article == null) return new SuggestTitleResponse { Success = false };

        var cfg = await GetEffectiveAsync();
        var content = TrimContent(article.Content, SuggestTitleContentLimit);
        var prompt = cfg.SuggestTitleTemplate
            .Replace("{Title}", article.Title)
            .Replace("{Content}", content);

        var (text, tokens, model, error) = await CallModelAsync(prompt, cfg);
        if (error != null || string.IsNullOrWhiteSpace(text))
        {
            await TryLogAsync(new AILog
            {
                ArticleId  = articleId,
                Action     = "suggest_title",
                PromptText = prompt,
                IsSuccess  = false,
                ErrorMsg   = error ?? "empty response",
                ModelUsed  = model
            }, "AILog save failed (suggest_title error)");
            return new SuggestTitleResponse { Success = false };
        }

        List<TitleSuggestion> titles;
        try
        {
            var match = Regex.Match(text, @"\[.*\]", RegexOptions.Singleline);
            if (!match.Success)
            {
                await TryLogAsync(new AILog
                {
                    ArticleId  = articleId,
                    Action     = "suggest_title",
                    PromptText = prompt,
                    ResultText = text,
                    IsSuccess  = false,
                    ErrorMsg   = "no JSON array found in response",
                    ModelUsed  = model
                }, "AILog save failed (suggest_title no-match)");
                return new SuggestTitleResponse { Success = false };
            }
            titles = JsonConvert.DeserializeObject<List<TitleSuggestion>>(match.Value) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SuggestTitlesAsync JSON parse failed (articleId={ArticleId})", articleId);
            await TryLogAsync(new AILog
            {
                ArticleId  = articleId,
                Action     = "suggest_title",
                PromptText = prompt,
                ResultText = text,
                IsSuccess  = false,
                ErrorMsg   = $"parse error: {ex.Message}",
                ModelUsed  = model
            }, "AILog save failed (suggest_title parse-exception)");
            return new SuggestTitleResponse { Success = false };
        }

        try
        {
            _db.AILogs.Add(new AILog
            {
                ArticleId  = articleId,
                Action     = "suggest_title",
                ResultText = text,
                ModelUsed  = model,
                TokensUsed = tokens,
                IsSuccess  = true
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Save failed after successful suggest_title (articleId={ArticleId})", articleId);
        }
        return new SuggestTitleResponse { Success = true, Titles = titles, Tokens = tokens };
    }

    /// <summary>
    /// [L] Kiểm duyệt nội dung — Score 0-100, Issues là list các vấn đề.
    /// </summary>
    // Đây là luồng xử lý kiểm duyệt nội dung bằng AI
    // Luồng: ghép prompt kiểm duyệt -> gọi Gemini -> parse JSON {score 0-100, issues[]} -> ghi AILog.
    //        (score>70 = vi phạm nặng; dùng ở bình luận & duyệt bài). Lỗi AI -> trả score=0 (không chặn).
    public async Task<ModerationResult> ModerateContentAsync(string articleContent, int? articleId = null)
    {
        var result = new ModerationResult { Score = 0, Issues = new List<string>() };
        if (string.IsNullOrWhiteSpace(articleContent))
            return result;

        var cfg = await GetEffectiveAsync();
        var clean = TrimContent(articleContent, ModerationContentLimit);
        var prompt = cfg.ModerateTemplate.Replace("{Content}", clean);

        var (text, tokens, model, error) = await CallModelAsync(prompt, cfg);
        if (error != null || string.IsNullOrWhiteSpace(text))
        {
            await TryLogAsync(new AILog
            {
                ArticleId  = articleId,
                Action     = "moderate",
                PromptText = prompt,
                IsSuccess  = false,
                ErrorMsg   = error ?? "empty response",
                ModelUsed  = model
            }, "AILog save failed (moderate error)");
            return result;
        }
        try
        {
            var match = Regex.Match(text, @"\{[\s\S]*?\}", RegexOptions.Singleline);
            if (!match.Success)
            {
                await TryLogAsync(new AILog
                {
                    ArticleId  = articleId,
                    Action     = "moderate",
                    PromptText = prompt,
                    ResultText = text,
                    IsSuccess  = false,
                    ErrorMsg   = "no JSON object found in response",
                    ModelUsed  = model
                }, "AILog save failed (moderate no-match)");
                return result;
            }
            var json = JObject.Parse(match.Value);
            result.Score = json["score"]?.Value<int>() ?? 0;
            if (json["issues"] is JArray issuesArr)
            {
                result.Issues = issuesArr
                    .Select(t => t?.ToString() ?? "")
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }
            await TryLogAsync(new AILog
            {
                ArticleId  = articleId,
                Action     = "moderate",
                ResultText = text,
                ModelUsed  = model,
                TokensUsed = tokens,
                IsSuccess  = true
            }, "Save failed after successful moderate");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ModerateContentAsync parse failed (articleId={ArticleId})", articleId);
            await TryLogAsync(new AILog
            {
                ArticleId  = articleId,
                Action     = "moderate",
                PromptText = prompt,
                ResultText = text,
                IsSuccess  = false,
                ErrorMsg   = $"parse error: {ex.Message}",
                ModelUsed  = model
            }, "AILog save failed (moderate parse-exception)");
            return result;
        }
    }

    /// <summary>
    /// [E] Chatbot: dùng SystemInstruction (persona) + ChatTemplate từ cấu hình.
    /// </summary>
    // Đây là luồng xử lý chatbot AI
    // Luồng: lấy cấu hình (SystemInstruction persona + giới hạn số câu) -> ghép câu hỏi -> gọi Gemini -> trả câu trả lời
    public async Task<(string reply, bool success)> ChatAsync(string userMessage)
    {
        //Kiểm tra input rỗng
        if (string.IsNullOrWhiteSpace(userMessage))
            return ("Vui lòng nhập câu hỏi.", false);
        //Lấy cấu hình AI
        var cfg = await GetEffectiveAsync();
        //Ghép prompt từ ChatTemplate, thay {MaxSentences} và {Question}
        var prompt = cfg.ChatTemplate
            .Replace("{MaxSentences}", cfg.ChatMaxSentences.ToString())
            .Replace("{Question}", userMessage);
        // gọi AI Model (Gemini/Groq) để trả lời 
        var (text, tokens, model, error) = await CallModelAsync(prompt, cfg, systemInstruction: cfg.SystemInstruction);
        // Xử lý lỗi hoặc kết quả rỗng, Ghi AILog thất bại, trả về thông báo lỗi cho người dùng
        if (error != null || string.IsNullOrWhiteSpace(text))
        {
            await TryLogAsync(new AILog
            {
                Action     = "chat",
                PromptText = userMessage,
                IsSuccess  = false,
                ErrorMsg   = error ?? "empty response",
                ModelUsed  = model
            }, "AILog save failed (chat error)");
            return ("Xin lỗi, hệ thống AI đang bận. Bạn thử lại sau nhé!", false);
        }
        //Ghi log thành công
        await TryLogAsync(new AILog
        {
            Action     = "chat",
            PromptText = userMessage,
            ResultText = text,
            ModelUsed  = model,
            TokensUsed = tokens,
            IsSuccess  = true
        }, "AILog save failed (chat success)");
        return (text!.Trim(), true);
    }

    /// <summary>
    /// Cắt content về giới hạn ký tự tại ranh giới câu/khoảng trắng gần nhất.
    /// </summary>
    private static string TrimContent(string? rawHtml, int limit)
    {
        if (string.IsNullOrEmpty(rawHtml)) return string.Empty;
        var stripped = Regex.Replace(rawHtml, "<[^>]+>", " ");
        stripped = Regex.Replace(stripped, @"\s+", " ").Trim();
        if (stripped.Length <= limit) return stripped;
        var slice = stripped[..limit];
        var searchStart = (int)(limit * 0.8);
        var lastPunct = slice.LastIndexOfAny(new[] { '.', '!', '?' }, slice.Length - 1, slice.Length - searchStart);
        if (lastPunct > 0) return slice[..(lastPunct + 1)];
        var lastSpace = slice.LastIndexOf(' ');
        return lastSpace > 0 ? slice[..lastSpace] : slice;
    }

    // Phân loại 1 bài vào danh mục CÓ SẴN bằng AI. Trả về CategoryId hoặc null nếu không xác định.
    // Ghi AILog riêng với Action = "classify_category".
    // Đây là luồng xử lý phân loại danh mục bằng AI (dùng khi nhập RSS không khớp từ khóa)
    // Luồng: ghép tiêu đề+tóm tắt+danh sách danh mục -> gọi Gemini -> trả CategoryId phù hợp (hoặc null)
    public async Task<int?> ClassifyCategoryAsync(string title, string summary, List<Category> categories)
    {
        if (categories == null || categories.Count == 0) return null;
        var cfg = await GetEffectiveAsync();

        var list = string.Join("\n", categories.Select((c, i) => $"{i + 1}. {c.Name}"));
        var prompt =
            "Bạn là bộ phân loại tin công nghệ. Dựa vào tiêu đề và tóm tắt bên dưới, hãy chọn DUY NHẤT " +
            "một danh mục phù hợp nhất trong danh sách. Chỉ trả về đúng TÊN danh mục (nguyên văn), không giải thích.\n\n" +
            $"Danh sách danh mục:\n{list}\n\n" +
            $"Tiêu đề: {title}\nTóm tắt: {summary}\n\nDanh mục phù hợp nhất:";

        var (text, tokens, model, error) = await CallModelAsync(prompt, cfg);
        if (error != null || string.IsNullOrWhiteSpace(text))
        {
            await TryLogAsync(new AILog
            {
                Action = "classify_category", PromptText = prompt,
                IsSuccess = false, ErrorMsg = error ?? "empty response", ModelUsed = model
            }, "AILog save failed (classify error)");
            return null;
        }

        var reply = text.Trim();
        var rn = SlugHelper.MakeSlug(reply);

        // Ưu tiên BẢNG ÁNH XẠ (admin cấu hình): nhãn AI -> danh mục hệ thống
        var maps = await _db.CategoryMappings.ToListAsync();
        var mp = maps.FirstOrDefault(m => SlugHelper.MakeSlug(m.AiLabel) == rn)
              ?? maps.FirstOrDefault(m => SlugHelper.MakeSlug(m.AiLabel).Length >= 2 && rn.Contains(SlugHelper.MakeSlug(m.AiLabel)));
        if (mp != null && categories.Any(c => c.Id == mp.CategoryId))
        {
            await TryLogAsync(new AILog
            {
                Action = "classify_category",
                ResultText = $"AI: \"{reply}\" → ánh xạ → {categories.First(c => c.Id == mp.CategoryId).Name}",
                ModelUsed = model, TokensUsed = tokens, IsSuccess = true
            }, "AILog save failed (classify-map)");
            return mp.CategoryId;
        }

        var match = categories.FirstOrDefault(c => SlugHelper.MakeSlug(c.Name) == rn)
                 ?? categories.FirstOrDefault(c => rn.Contains(SlugHelper.MakeSlug(c.Name)))
                 ?? categories.FirstOrDefault(c => SlugHelper.MakeSlug(c.Name).Contains(rn) && rn.Length >= 3);

        await TryLogAsync(new AILog
        {
            Action = "classify_category",
            ResultText = $"AI trả lời: \"{reply}\" => {(match?.Name ?? "(không khớp danh mục)")}",
            ModelUsed = model, TokensUsed = tokens, IsSuccess = match != null
        }, "AILog save failed (classify)");
        return match?.Id;
    }

    private async Task TryLogAsync(AILog log, string warningContext)
    {
        try
        {
            _db.AILogs.Add(log);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, warningContext);
        }
    }

    private async Task<(string? text, int tokens, string model, string? error)> CallGeminiAsync(
        string prompt, AiSetting cfg, string? systemInstruction = null)
    {
        var apiKey     = _config["Gemini:ApiKey"];
        var model      = string.IsNullOrWhiteSpace(cfg.Model) ? DefaultGeminiModel : cfg.Model;
        var apiVersion = string.IsNullOrWhiteSpace(cfg.ApiVersion) ? "v1beta" : cfg.ApiVersion;
        if (string.IsNullOrWhiteSpace(apiKey))
            return (null, 0, model, "Gemini API key chưa cấu hình");

        var url = $"https://generativelanguage.googleapis.com/{apiVersion}/{model}:generateContent?key={apiKey}";

        var generationConfig = new
        {
            maxOutputTokens = cfg.MaxOutputTokens > 0 ? cfg.MaxOutputTokens : 2048,
            temperature     = cfg.Temperature,
            thinkingConfig  = new { thinkingBudget = cfg.ThinkingBudget }
        };
        var userTurn = new[] { new { role = "user", parts = new[] { new { text = prompt } } } };
        object body = string.IsNullOrWhiteSpace(systemInstruction)
            ? new { contents = userTurn, generationConfig }
            : new { systemInstruction = new { parts = new[] { new { text = systemInstruction } } }, contents = userTurn, generationConfig };

        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            var json = JsonConvert.SerializeObject(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);
            var resStr = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini HTTP {Status}: {Body}", (int)response.StatusCode, resStr);
                return (null, 0, model, $"HTTP {(int)response.StatusCode}");
            }
            var parsed = JObject.Parse(resStr);
            var text   = parsed["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
            var tokens = parsed["usageMetadata"]?["totalTokenCount"]?.Value<int>() ?? 0;
            if (text != null)
                text = Regex.Replace(text, @"```\w*", "").Trim();
            return (text, tokens, model, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CallGeminiAsync exception");
            return (null, 0, model, ex.Message);
        }
    }

    // ===== GROQ (dự phòng khi Gemini lỗi/hết quota) — API chuẩn OpenAI =====
    private async Task<(string? text, int tokens, string model, string? error)> CallGroqAsync(
        string prompt, string? systemInstruction = null)
    {
        var apiKey = _config["Groq:ApiKey"];
        var model  = string.IsNullOrWhiteSpace(_config["Groq:Model"]) ? "llama-3.3-70b-versatile" : _config["Groq:Model"]!;
        if (string.IsNullOrWhiteSpace(apiKey))
            return (null, 0, "groq:" + model, "Groq API key chưa cấu hình");

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(systemInstruction))
            messages.Add(new { role = "system", content = systemInstruction });
        messages.Add(new { role = "user", content = prompt });
        object body = new { model, messages };

        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            var json = JsonConvert.SerializeObject(body);
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.SendAsync(req);
            var resStr = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq HTTP {Status}: {Body}", (int)response.StatusCode, resStr);
                return (null, 0, "groq:" + model, $"HTTP {(int)response.StatusCode}");
            }
            var parsed = JObject.Parse(resStr);
            var text   = parsed["choices"]?[0]?["message"]?["content"]?.ToString();
            var tokens = parsed["usage"]?["total_tokens"]?.Value<int>() ?? 0;
            if (text != null)
                text = Regex.Replace(text, @"```\w*", "").Trim();
            return (text, tokens, "groq:" + model, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CallGroqAsync exception");
            return (null, 0, "groq:" + model, ex.Message);
        }
    }

    // ===== Điều phối: Gemini trước; lỗi/rỗng -> Groq (nếu đã cấu hình key) =====
    private async Task<(string? text, int tokens, string model, string? error)> CallModelAsync(
        string prompt, AiSetting cfg, string? systemInstruction = null)
    {
        var (text, tokens, model, error) = await CallGeminiAsync(prompt, cfg, systemInstruction);
        if (error == null && !string.IsNullOrWhiteSpace(text))
            return (text, tokens, model, null);

        if (!string.IsNullOrWhiteSpace(_config["Groq:ApiKey"]))
        {
            _logger.LogWarning("Gemini lỗi ({Err}) -> chuyển sang Groq", error);
            var (gText, gTokens, gModel, gError) = await CallGroqAsync(prompt, systemInstruction);
            if (gError == null && !string.IsNullOrWhiteSpace(gText))
                return (gText, gTokens, gModel + " (fallback)", null);
            return (null, 0, gModel + " (fallback)", $"Gemini: {error}; Groq: {gError}");
        }
        return (text, tokens, model, error);
    }
}
