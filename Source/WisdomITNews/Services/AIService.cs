using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WisdomITNews.Data;
using WisdomITNews.Models;
using Microsoft.EntityFrameworkCore;

namespace WisdomITNews.Services;

public class AIService
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly ILogger<AIService> _logger;

    public AIService(
        IHttpClientFactory http,
        IConfiguration config,
        AppDbContext db,
        ILogger<AIService> logger)
    {
        _http = http;
        _config = config;
        _db = db;
        _logger = logger;
    }

    public async Task<SummarizeResponse> SummarizeAsync(int articleId)
    {
        var article = await _db.Articles.FindAsync(articleId);
        if (article == null) return new SummarizeResponse { Success = false };

        if (!string.IsNullOrEmpty(article.AiSummary))
            return new SummarizeResponse { Success = true, Summary = article.AiSummary, Cached = true };

        var content = Regex.Replace(article.Content, "<[^>]+>", "");
        content = content.Length > 3000 ? content[..3000] : content;

        var prompt = $"Tóm tắt bài báo sau bằng tiếng Việt (150-200 từ), dùng HTML với <p> và <ul><li>:\n\nTiêu đề: {article.Title}\n\nNội dung: {content}";

        var (text, tokens, error) = await CallGeminiAsync(prompt);

        if (error != null || string.IsNullOrWhiteSpace(text))
        {
            try
            {
                _db.AILogs.Add(new AILog
                {
                    ArticleId  = articleId,
                    Action     = "summarize",
                    PromptText = prompt,
                    IsSuccess  = false,
                    ErrorMsg   = error ?? "empty response",
                    ModelUsed  = _config["Gemini:Model"]
                });
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AILog save failed (summarize)");
            }
            return new SummarizeResponse { Success = false };
        }

        article.AiSummary = text;
        article.UpdatedAt = DateTime.Now;
        _db.AILogs.Add(new AILog
        {
            ArticleId  = articleId,
            Action     = "summarize",
            ResultText = text,
            ModelUsed  = _config["Gemini:Model"],
            TokensUsed = tokens,
            IsSuccess  = true
        });
        await _db.SaveChangesAsync();

        return new SummarizeResponse { Success = true, Summary = text!, Tokens = tokens };
    }

    public async Task<SuggestTitleResponse> SuggestTitlesAsync(int articleId)
    {
        var article = await _db.Articles.FindAsync(articleId);
        if (article == null) return new SuggestTitleResponse { Success = false };

        var content = Regex.Replace(article.Content, "<[^>]+>", "");
        content = content.Length > 2000 ? content[..2000] : content;

        var prompt = $@"Gợi ý 5 tiêu đề hấp dẫn cho bài báo sau bằng tiếng Việt, mỗi tiêu đề theo phong cách khác nhau: thông tin, tò mò, câu hỏi, ngắn gọn, có số liệu.
Trả về JSON (không markdown): [{{""style"":""tên"",""text"":""tiêu đề""}}]

Bài: {article.Title}
{content}";

        var (text, tokens, error) = await CallGeminiAsync(prompt);

        if (error != null || string.IsNullOrWhiteSpace(text))
            return new SuggestTitleResponse { Success = false };

        try
        {
            var match = Regex.Match(text, @"\[.*\]", RegexOptions.Singleline);
            if (!match.Success) return new SuggestTitleResponse { Success = false };
            var titles = JsonConvert.DeserializeObject<List<TitleSuggestion>>(match.Value) ?? new();

            _db.AILogs.Add(new AILog
            {
                ArticleId  = articleId,
                Action     = "suggest_title",
                ResultText = text,
                ModelUsed  = _config["Gemini:Model"],
                TokensUsed = tokens,
                IsSuccess  = true
            });
            await _db.SaveChangesAsync();

            return new SuggestTitleResponse { Success = true, Titles = titles, Tokens = tokens };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SuggestTitlesAsync parse failed");
            return new SuggestTitleResponse { Success = false };
        }
    }

    /// <summary>
    /// [L] Kiểm duyệt nội dung bài viết — Score 0-100, Issues là list các vấn đề.
    /// </summary>
    public async Task<ModerationResult> ModerateContentAsync(string articleContent)
    {
        var result = new ModerationResult { Score = 0, Issues = new List<string>() };

        if (string.IsNullOrWhiteSpace(articleContent))
            return result;

        var clean = Regex.Replace(articleContent, "<[^>]+>", " ");
        clean = clean.Length > 4000 ? clean[..4000] : clean;

        var prompt = $@"Bạn là kiểm duyệt viên nội dung báo điện tử.
Phân tích đoạn văn sau và trả về JSON đúng format (KHÔNG có markdown, KHÔNG có ```):
{{""score"": <số 0-100>, ""issues"": [""...""]}}

Quy tắc:
- score > 70  = nội dung vi phạm nghiêm trọng (bạo lực, ngôn từ thù ghét, kích động chính trị phản động, thông tin sai lệch nghiêm trọng)
- score 40-70 = có dấu hiệu cần xem xét (ngôn từ cảm tính, tin chưa kiểm chứng, một số chi tiết nhạy cảm)
- score < 40  = nội dung an toàn

Nội dung:
{clean}";

        var (text, _, error) = await CallGeminiAsync(prompt);

        if (error != null || string.IsNullOrWhiteSpace(text)) return result;

        try
        {
            var match = Regex.Match(text, @"\{[\s\S]*?\}", RegexOptions.Singleline);
            if (!match.Success) return result;

            var json = JObject.Parse(match.Value);
            result.Score = json["score"]?.Value<int>() ?? 0;
            if (json["issues"] is JArray issuesArr)
            {
                result.Issues = issuesArr
                    .Select(t => t?.ToString() ?? "")
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ModerateContentAsync parse failed");
            return result;
        }
    }

    /// <summary>
    /// [E] Chatbot: trả lời câu hỏi của người dùng dưới phong cách báo IT.
    /// </summary>
    public async Task<(string reply, bool success)> ChatAsync(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return ("Vui lòng nhập câu hỏi.", false);

        var prompt = $@"Bạn là trợ lý ảo của trang ""Wisdom IT News"" — báo điện tử về công nghệ, lập trình và AI.
Trả lời ngắn gọn (tối đa 4 câu) bằng tiếng Việt, thân thiện và chính xác.
Nếu câu hỏi không liên quan đến công nghệ/IT/AI, gợi ý người dùng tìm kiếm bài viết phù hợp trong site.

Câu hỏi: {userMessage}";

        var (text, _, error) = await CallGeminiAsync(prompt);

        if (error != null || string.IsNullOrWhiteSpace(text))
        {
            try
            {
                _db.AILogs.Add(new AILog
                {
                    Action     = "chat",
                    PromptText = userMessage,
                    IsSuccess  = false,
                    ErrorMsg   = error ?? "empty response"
                });
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AILog save failed (chat error)");
            }
            return ("Xin lỗi, hệ thống AI đang bận. Bạn thử lại sau nhé!", false);
        }

        try
        {
            _db.AILogs.Add(new AILog
            {
                Action     = "chat",
                PromptText = userMessage,
                ResultText = text,
                ModelUsed  = _config["Gemini:Model"],
                IsSuccess  = true
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AILog save failed (chat success)");
        }

        return (text!.Trim(), true);
    }

    private async Task<(string? text, int tokens, string? error)> CallGeminiAsync(string prompt)
    {
        var apiKey = _config["Gemini:ApiKey"];
        var model  = _config["Gemini:Model"] ?? "models/gemini-2.5-flash";
        if (string.IsNullOrWhiteSpace(apiKey))
            return (null, 0, "Gemini API key chưa cấu hình");

        var url = $"https://generativelanguage.googleapis.com/v1/{model}:generateContent?key={apiKey}";

        var body = new
        {
            contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
            generationConfig = new { maxOutputTokens = 1000, temperature = 0.7 }
        };

        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            var json = JsonConvert.SerializeObject(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);
            var resStr = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return (null, 0, $"HTTP {(int)response.StatusCode}");

            var parsed = JObject.Parse(resStr);
            var text   = parsed["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
            var tokens = parsed["usageMetadata"]?["totalTokenCount"]?.Value<int>() ?? 0;
            if (text != null)
                text = Regex.Replace(text, @"```\w*", "").Trim();
            return (text, tokens, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CallGeminiAsync failed");
            return (null, 0, ex.Message);
        }
    }
}
