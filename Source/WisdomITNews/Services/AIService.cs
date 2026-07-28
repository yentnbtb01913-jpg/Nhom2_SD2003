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
    // Hàm tóm tắt 1 bài viết bằng AI. Trả về SummarizeResponse (thành công?, đoạn tóm tắt, có cache không, số token).
    public async Task<SummarizeResponse> SummarizeAsync(int articleId)
    {
        // Lấy bài viết theo id.
        var article = await _db.Articles.FindAsync(articleId);
        // Không tìm thấy bài → trả về thất bại.
        if (article == null) return new SummarizeResponse { Success = false };

        // === CACHE: nếu bài ĐÃ có tóm tắt (AiSummary) rồi thì trả luôn, KHÔNG gọi AI lại (tiết kiệm token/chi phí) ===
        if (!string.IsNullOrEmpty(article.AiSummary))
            return new SummarizeResponse { Success = true, Summary = article.AiSummary, Cached = true }; // Cached=true: lấy từ bộ nhớ đệm

        // Lấy cấu hình AI đang áp dụng (model, mẫu prompt, độ dài tóm tắt...) từ AiSetting.
        var cfg = await GetEffectiveAsync();

        // Cắt bớt nội dung nếu quá dài (giới hạn SummarizeContentLimit) để tránh vượt token.
        var content = TrimContent(article.Content, SummarizeContentLimit);

        // Dựng prompt: lấy mẫu SummarizeTemplate trong cấu hình và thay các chỗ giữ chỗ:
        var prompt = cfg.SummarizeTemplate
            .Replace("{Length}", cfg.SummarizeLength)  // {Length} -> độ dài mong muốn (vd "3 câu")
            .Replace("{Title}", article.Title)         // {Title}  -> tiêu đề bài
            .Replace("{Content}", content);            // {Content}-> nội dung đã cắt

        // Gọi mô hình AI với prompt. Trả về: text tóm tắt, số token, tên model, lỗi (nếu có).
        var (text, tokens, model, error) = await CallModelAsync(prompt, cfg);

        // === AI lỗi hoặc trả rỗng → ghi log thất bại rồi trả về Success=false ===
        if (error != null || string.IsNullOrWhiteSpace(text))
        {
            await TryLogAsync(new AILog
            {
                ArticleId = articleId,                 // gắn log với bài nào
                Action = "summarize",               // loại hành động: tóm tắt
                PromptText = prompt,                     // lưu prompt đã gửi (debug)
                IsSuccess = false,                      // đánh dấu thất bại
                ErrorMsg = error ?? "empty response",  // lý do lỗi
                ModelUsed = model                       // model đã dùng
            }, "AILog save failed (summarize error)");
            return new SummarizeResponse { Success = false };
        }

        // === Thành công: LƯU tóm tắt vào bài để lần sau dùng lại (chính là tạo cache) ===
        article.AiSummary = text;              // gán đoạn tóm tắt vào cột AiSummary của bài
        article.UpdatedAt = DateTime.Now;      // cập nhật mốc thời gian sửa

        try
        {
            // Ghi AILog thành công (kết quả, model, số token) để minh bạch/theo dõi chi phí.
            _db.AILogs.Add(new AILog
            {
                ArticleId = articleId,
                Action = "summarize",
                ResultText = text,
                ModelUsed = model,
                TokensUsed = tokens,
                IsSuccess = true
            });
            // Lưu cả tóm tắt (article.AiSummary) và log xuống DB trong 1 lần SaveChanges.
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Nếu lưu DB lỗi → chỉ cảnh báo, KHÔNG ném lỗi ra ngoài (vẫn trả tóm tắt cho người dùng).
            _logger.LogWarning(ex, "Save failed after successful summarize (articleId={ArticleId})", articleId);
        }

        // Trả về tóm tắt cho nơi gọi (text! nghĩa là chắc chắn không null vì đã kiểm tra ở trên).
        return new SummarizeResponse { Success = true, Summary = text!, Tokens = tokens };
    }

    // Đây là luồng xử lý gợi ý tiêu đề bài viết bằng AI
    // Luồng: lấy cấu hình + cắt nội dung -> gọi Gemini -> parse JSON 5 tiêu đề (5 phong cách) -> ghi AILog
    // Hàm gợi ý tiêu đề cho 1 bài viết bằng AI. Trả về SuggestTitleResponse (thành công?, danh sách tiêu đề, số token).
    public async Task<SuggestTitleResponse> SuggestTitlesAsync(int articleId)
    {
        // Lấy bài viết theo id.
        var article = await _db.Articles.FindAsync(articleId);
        // Không tìm thấy bài → trả thất bại. (Lưu ý: hàm này KHÔNG cache, mỗi lần bấm là gọi AI lại.)
        if (article == null) return new SuggestTitleResponse { Success = false };

        // Lấy cấu hình AI đang áp dụng (model, mẫu prompt gợi ý tiêu đề...).
        var cfg = await GetEffectiveAsync();

        // Cắt bớt nội dung nếu quá dài (giới hạn SuggestTitleContentLimit) để tránh vượt token.
        var content = TrimContent(article.Content, SuggestTitleContentLimit);

        // Dựng prompt từ mẫu SuggestTitleTemplate, thay {Title} = tiêu đề hiện tại, {Content} = nội dung đã cắt.
        var prompt = cfg.SuggestTitleTemplate
            .Replace("{Title}", article.Title)
            .Replace("{Content}", content);

        // Gọi mô hình AI. Trả về: text kết quả, số token, tên model, lỗi (nếu có).
        var (text, tokens, model, error) = await CallModelAsync(prompt, cfg);

        // === AI lỗi hoặc trả rỗng → ghi log thất bại rồi trả Success=false ===
        if (error != null || string.IsNullOrWhiteSpace(text))
        {
            await TryLogAsync(new AILog
            {
                ArticleId = articleId,
                Action = "suggest_title",           // loại hành động: gợi ý tiêu đề
                PromptText = prompt,
                IsSuccess = false,
                ErrorMsg = error ?? "empty response",
                ModelUsed = model
            }, "AILog save failed (suggest_title error)");
            return new SuggestTitleResponse { Success = false };
        }

        // Biến chứa danh sách tiêu đề sẽ bóc ra từ phản hồi.
        List<TitleSuggestion> titles;

        // === BÓC TÁCH JSON MẢNG tiêu đề (khác Summarize: ở đây kết quả là một MẢNG [ ... ]) ===
        try
        {
            // Regex bắt khối MẢNG JSON đầu tiên dạng [ ... ] (phòng khi AI trả kèm chữ thừa quanh mảng).
            var match = Regex.Match(text, @"\[.*\]", RegexOptions.Singleline);

            // Nếu không tìm thấy mảng JSON → coi như không parse được.
            if (!match.Success)
            {
                await TryLogAsync(new AILog
                {
                    ArticleId = articleId,
                    Action = "suggest_title",
                    PromptText = prompt,
                    ResultText = text,                     // lưu phản hồi thô để xem AI trả gì
                    IsSuccess = false,
                    ErrorMsg = "no JSON array found in response",
                    ModelUsed = model
                }, "AILog save failed (suggest_title no-match)");
                return new SuggestTitleResponse { Success = false };
            }

            // Chuyển chuỗi mảng JSON thành List<TitleSuggestion>; nếu ra null thì dùng list rỗng.
            titles = JsonConvert.DeserializeObject<List<TitleSuggestion>>(match.Value) ?? new();
        }
        // Nếu parse JSON ném lỗi (định dạng sai...) → xử lý ngoại lệ.
        catch (Exception ex)
        {
            // Ghi cảnh báo ra logger hệ thống.
            _logger.LogWarning(ex, "SuggestTitlesAsync JSON parse failed (articleId={ArticleId})", articleId);
            // Ghi AILog thất bại kèm thông điệp lỗi cụ thể.
            await TryLogAsync(new AILog
            {
                ArticleId = articleId,
                Action = "suggest_title",
                PromptText = prompt,
                ResultText = text,
                IsSuccess = false,
                ErrorMsg = $"parse error: {ex.Message}",
                ModelUsed = model
            }, "AILog save failed (suggest_title parse-exception)");
            return new SuggestTitleResponse { Success = false };
        }

        // === Ghi AILog THÀNH CÔNG (không lưu tiêu đề vào bài — chỉ gợi ý để nhà báo tự chọn) ===
        try
        {
            _db.AILogs.Add(new AILog
            {
                ArticleId = articleId,
                Action = "suggest_title",
                ResultText = text,        // phản hồi thô của AI
                ModelUsed = model,
                TokensUsed = tokens,      // số token đã dùng (theo dõi chi phí)
                IsSuccess = true
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Lưu log lỗi → chỉ cảnh báo, không ném ra ngoài (vẫn trả danh sách tiêu đề cho người dùng).
            _logger.LogWarning(ex, "Save failed after successful suggest_title (articleId={ArticleId})", articleId);
        }

        // Trả danh sách tiêu đề gợi ý + số token cho nơi gọi (Controller -> đổ ra editor để nhà báo chọn).
        return new SuggestTitleResponse { Success = true, Titles = titles, Tokens = tokens };
    }

    /// <summary>
    /// [L] Kiểm duyệt nội dung — Score 0-100, Issues là list các vấn đề.
    /// </summary>
    // Đây là luồng xử lý kiểm duyệt nội dung bằng AI
    // Luồng: ghép prompt kiểm duyệt -> gọi Gemini -> parse JSON {score 0-100, issues[]} -> ghi AILog.
    //        (score>70 = vi phạm nặng; dùng ở bình luận & duyệt bài). Lỗi AI -> trả score=0 (không chặn).
    // Hàm kiểm duyệt nội dung bằng AI. Trả về ModerationResult (điểm vi phạm + danh sách ngôn từ vi phạm).
    // articleContent: nội dung cần kiểm; articleId: id bài (tùy chọn, để ghi log gắn đúng bài).
    public async Task<ModerationResult> ModerateContentAsync(string articleContent, int? articleId = null)
    {
        // Tạo sẵn kết quả mặc định: điểm = 0 (không vi phạm), danh sách vi phạm rỗng.
        var result = new ModerationResult { Score = 0, Issues = new List<string>() };

        // Nếu nội dung rỗng/toàn khoảng trắng → không cần gọi AI, trả kết quả "sạch" luôn.
        if (string.IsNullOrWhiteSpace(articleContent))
            return result;

        // Lấy cấu hình AI đang áp dụng (model, prompt template, tham số...) từ bảng AiSetting.
        var cfg = await GetEffectiveAsync();

        // Cắt bớt nội dung nếu quá dài (giới hạn ModerationContentLimit) để tiết kiệm token / tránh lỗi.
        var clean = TrimContent(articleContent, ModerationContentLimit);

        // Dựng prompt: lấy mẫu prompt kiểm duyệt trong cấu hình, thay chỗ giữ chỗ {Content} bằng nội dung thật.
        var prompt = cfg.ModerateTemplate.Replace("{Content}", clean);

        // Gọi mô hình AI (Gemini/Groq) với prompt vừa dựng. Trả về: văn bản kết quả, số token, tên model, lỗi (nếu có).
        var (text, tokens, model, error) = await CallModelAsync(prompt, cfg);

        // === Trường hợp 1: AI lỗi hoặc trả về rỗng ===
        if (error != null || string.IsNullOrWhiteSpace(text))
        {
            // Ghi AILog đánh dấu THẤT BẠI để truy vết (best-effort, lỗi ghi log không làm hỏng luồng chính).
            await TryLogAsync(new AILog
            {
                ArticleId = articleId,                 // gắn với bài nào
                Action = "moderate",                // loại hành động: kiểm duyệt
                PromptText = prompt,                     // lưu prompt đã gửi (để debug)
                IsSuccess = false,                      // đánh dấu thất bại
                ErrorMsg = error ?? "empty response",  // lý do: lỗi trả về, hoặc "rỗng"
                ModelUsed = model                       // model đã dùng
            }, "AILog save failed (moderate error)");
            // Trả kết quả mặc định (điểm 0) → KHÔNG chặn người dùng khi AI hỏng (best-effort).
            return result;
        }

        // === Trường hợp 2: AI có trả về → thử bóc tách JSON ===
        try
        {
            // Dùng regex bắt khối JSON đầu tiên dạng { ... } (phòng khi AI trả kèm chữ/markdown thừa quanh JSON).
            var match = Regex.Match(text, @"\{[\s\S]*?\}", RegexOptions.Singleline);

            // Nếu không tìm thấy khối JSON nào trong phản hồi → coi như không parse được.
            if (!match.Success)
            {
                // Ghi AILog thất bại với lý do "không có JSON".
                await TryLogAsync(new AILog
                {
                    ArticleId = articleId,
                    Action = "moderate",
                    PromptText = prompt,
                    ResultText = text,                    // lưu cả phản hồi thô để xem AI trả gì
                    IsSuccess = false,
                    ErrorMsg = "no JSON object found in response",
                    ModelUsed = model
                }, "AILog save failed (moderate no-match)");
                // Trả kết quả mặc định (không chặn).
                return result;
            }

            // Parse chuỗi JSON vừa bắt được thành object để đọc các trường.
            var json = JObject.Parse(match.Value);

            // Đọc trường "score" (điểm vi phạm). Nếu thiếu/không phải số thì mặc định 0.
            result.Score = json["score"]?.Value<int>() ?? 0;

            // Nếu "issues" là một mảng JSON (danh sách ngôn từ vi phạm) thì đọc vào Issues.
            if (json["issues"] is JArray issuesArr)
            {
                result.Issues = issuesArr
                    .Select(t => t?.ToString() ?? "")            // đổi từng phần tử thành chuỗi
                    .Where(s => !string.IsNullOrWhiteSpace(s))   // bỏ phần tử rỗng
                    .ToList();                                   // gom thành List<string>
            }

            // Ghi AILog THÀNH CÔNG (lưu kết quả, model, số token) để minh bạch quyết định của AI.
            await TryLogAsync(new AILog
            {
                ArticleId = articleId,
                Action = "moderate",
                ResultText = text,        // phản hồi thô của AI
                ModelUsed = model,
                TokensUsed = tokens,      // số token đã dùng (theo dõi chi phí)
                IsSuccess = true
            }, "Save failed after successful moderate");

            // Trả kết quả đã có Score + Issues cho nơi gọi (Controller) quyết định ẩn/cảnh báo.
            return result;
        }
        // === Trường hợp 3: có ngoại lệ khi parse (JSON sai định dạng...) ===
        catch (Exception ex)
        {
            // Ghi cảnh báo ra logger hệ thống.
            _logger.LogWarning(ex, "ModerateContentAsync parse failed (articleId={ArticleId})", articleId);

            // Ghi AILog thất bại kèm thông điệp lỗi cụ thể để truy vết.
            await TryLogAsync(new AILog
            {
                ArticleId = articleId,
                Action = "moderate",
                PromptText = prompt,
                ResultText = text,
                IsSuccess = false,
                ErrorMsg = $"parse error: {ex.Message}",
                ModelUsed = model
            }, "AILog save failed (moderate parse-exception)");

            // Vẫn trả kết quả mặc định → không làm hỏng thao tác của người dùng.
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
    // Phân loại danh mục cho 1 bài. Trả về CategoryId phù hợp (int?), null nếu không xác định được.
    // title: tiêu đề, summary: tóm tắt, categories: danh sách danh mục hiện có của hệ thống.
    // [ĐÃ GỠ] ClassifyCategoryAsync — bỏ AI phân loại danh mục. Nguồn RSS gắn danh mục cố định.


    // Hàm ghi 1 bản ghi AILog (best-effort): lỗi ghi log KHÔNG được làm hỏng luồng chính.
    private async Task TryLogAsync(AILog log, string warningContext)
    {
        try
        {
            _db.AILogs.Add(log);              // thêm bản ghi log vào context
            await _db.SaveChangesAsync();     // lưu xuống DB
        }
        catch (Exception ex)
        {
            // Nếu ghi log lỗi (vd bảng chưa có) → chỉ cảnh báo ra logger, nuốt lỗi, không ném ra ngoài.
            _logger.LogWarning(ex, warningContext);
        }
    }


    // Hàm gọi trực tiếp Google Gemini bằng HttpClient thô (không dùng SDK).
    // Trả về tuple: (text kết quả, số token, tên model, lỗi).
    private async Task<(string? text, int tokens, string model, string? error)> CallGeminiAsync(
        string prompt, AiSetting cfg, string? systemInstruction = null)
    {
        // Đọc khóa API Gemini từ appsettings.json (KHÔNG hard-code trong code).
        var apiKey = _config["Gemini:ApiKey"];
        // Lấy model từ cấu hình; nếu trống thì dùng model mặc định.
        var model = string.IsNullOrWhiteSpace(cfg.Model) ? DefaultGeminiModel : cfg.Model;
        // Lấy phiên bản API; mặc định "v1beta".
        var apiVersion = string.IsNullOrWhiteSpace(cfg.ApiVersion) ? "v1beta" : cfg.ApiVersion;

        // Chưa cấu hình khóa → trả lỗi ngay, không gọi mạng.
        if (string.IsNullOrWhiteSpace(apiKey))
            return (null, 0, model, "Gemini API key chưa cấu hình");

        // Dựng URL endpoint generateContent của Gemini, kèm khóa API trên query string.
        var url = $"https://generativelanguage.googleapis.com/{apiVersion}/{model}:generateContent?key={apiKey}";

        // Cấu hình sinh nội dung: giới hạn token đầu ra, độ ngẫu nhiên (temperature), ngân sách "suy nghĩ".
        var generationConfig = new
        {
            maxOutputTokens = cfg.MaxOutputTokens > 0 ? cfg.MaxOutputTokens : 2048, // mặc định 2048 nếu chưa đặt
            temperature = cfg.Temperature,
            thinkingConfig = new { thinkingBudget = cfg.ThinkingBudget }
        };

        // Lượt hội thoại của người dùng: gói prompt vào cấu trúc contents mà Gemini yêu cầu.
        var userTurn = new[] { new { role = "user", parts = new[] { new { text = prompt } } } };

        // Dựng body request: nếu KHÔNG có systemInstruction thì chỉ gửi contents;
        // nếu CÓ thì thêm phần systemInstruction (chỉ dẫn hệ thống, vd giới hạn phạm vi chatbot).
        object body = string.IsNullOrWhiteSpace(systemInstruction)
            ? new { contents = userTurn, generationConfig }
            : new { systemInstruction = new { parts = new[] { new { text = systemInstruction } } }, contents = userTurn, generationConfig };

        try
        {
            // Tạo HttpClient từ factory và đặt timeout 20 giây (tránh treo nếu Gemini phản hồi chậm).
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            // Chuyển body thành chuỗi JSON.
            var json = JsonConvert.SerializeObject(body);
            // Gói JSON thành nội dung HTTP (UTF-8, kiểu application/json).
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Gửi POST tới Gemini.
            var response = await client.PostAsync(url, content);
            // Đọc toàn bộ chuỗi phản hồi.
            var resStr = await response.Content.ReadAsStringAsync();

            // Nếu HTTP != 2xx (lỗi) → ghi cảnh báo và trả lỗi kèm mã HTTP.
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini HTTP {Status}: {Body}", (int)response.StatusCode, resStr);
                return (null, 0, model, $"HTTP {(int)response.StatusCode}");
            }

            // Parse JSON phản hồi để lấy dữ liệu.
            var parsed = JObject.Parse(resStr);
            // Lấy văn bản trả lời: đi vào candidates[0].content.parts[0].text (dùng ?. để tránh null).
            var text = parsed["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
            // Lấy tổng số token đã dùng (nếu có) để theo dõi chi phí; không có thì 0.
            var tokens = parsed["usageMetadata"]?["totalTokenCount"]?.Value<int>() ?? 0;

            // Nếu có text: loại bỏ hàng rào code markdown ```json / ``` mà LLM hay thêm, rồi trim.
            if (text != null)
                text = Regex.Replace(text, @"```\w*", "").Trim();

            // Trả về kết quả thành công (không lỗi).
            return (text, tokens, model, null);
        }
        catch (Exception ex)
        {
            // Ngoại lệ mạng/parse → ghi cảnh báo và trả lỗi (best-effort, không ném ra ngoài).
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
