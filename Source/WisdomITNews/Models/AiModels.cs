namespace WisdomITNews.Models;

// ===== Cấu hình AI lưu DB (1 dòng, Id=1) — chỉnh sống ở trang "Quản lý AI" =====
public class AiSetting
{
    public int Id { get; set; }

    // Tham số chung — áp cho MỌI lần gọi Gemini
    public string Model { get; set; } = "models/gemini-2.5-flash";
    public string ApiVersion { get; set; } = "v1beta";
    public double Temperature { get; set; } = 0.7;
    public int MaxOutputTokens { get; set; } = 2048;
    public int ThinkingBudget { get; set; } = 0; // 0 = tắt "thinking" của 2.5-flash

    // Chỉ dẫn hệ thống (persona) cho chatbot — vd: "Bạn là AI của Wisdom..."
    public string SystemInstruction { get; set; } = "Bạn là trợ lý ảo của Wisdom IT News — báo điện tử về công nghệ, lập trình và AI. Thân thiện và chính xác.";

    // Prompt từng tác vụ. Placeholder: {Length} {Title} {Content} {MaxSentences} {Question}
    public string SummarizeLength { get; set; } = "150-200 từ";
    public string SummarizeTemplate { get; set; } = AiDefaults.Summarize;
    public string SuggestTitleTemplate { get; set; } = AiDefaults.SuggestTitle;
    public string ModerateTemplate { get; set; } = AiDefaults.Moderate;
    public int ChatMaxSentences { get; set; } = 4;
    public string ChatTemplate { get; set; } = AiDefaults.Chat;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

// ===== Options pattern (Mức 1): bind từ appsettings "AI" — mặc định/dự phòng =====
public class AiOptions
{
    public string Model { get; set; } = "models/gemini-2.5-flash";
    public string ApiVersion { get; set; } = "v1beta";
    public double Temperature { get; set; } = 0.7;
    public int MaxOutputTokens { get; set; } = 2048;
    public int ThinkingBudget { get; set; } = 0;
    public string SystemInstruction { get; set; } = "";
    public AiTaskOptions Summarize { get; set; } = new();
    public AiTaskOptions SuggestTitle { get; set; } = new();
    public AiTaskOptions Moderate { get; set; } = new();
    public AiTaskOptions Chat { get; set; } = new();
}

public class AiTaskOptions
{
    public string Length { get; set; } = "";
    public int MaxSentences { get; set; } = 4;
    public string Template { get; set; } = "";
}

// ===== Prompt mặc định (dùng khi chưa có dòng DB / appsettings trống) =====
public static class AiDefaults
{
    public const string Summarize =
        "Tóm tắt bài báo sau bằng tiếng Việt ({Length}), dùng HTML với <p> và <ul><li>:\n\nTiêu đề: {Title}\n\nNội dung: {Content}";

    public const string SuggestTitle =
        "Gợi ý 5 tiêu đề hấp dẫn cho bài báo sau bằng tiếng Việt, mỗi tiêu đề theo phong cách khác nhau: thông tin, tò mò, câu hỏi, ngắn gọn, có số liệu.\nTrả về JSON (không markdown): [{\"style\":\"tên\",\"text\":\"tiêu đề\"}]\nBài: {Title}\n{Content}";

    public const string Moderate =
        "Bạn là kiểm duyệt viên nội dung báo điện tử.\nPhân tích đoạn văn sau và trả về JSON đúng format (KHÔNG markdown):\n{\"score\": <số 0-100>, \"issues\": [\"...\"]}\nQuy tắc:\n- score > 70 = vi phạm nghiêm trọng (bạo lực, thù ghét, kích động chính trị phản động, sai lệch nghiêm trọng)\n- score 40-70 = cần xem xét (cảm tính, chưa kiểm chứng, chi tiết nhạy cảm)\n- score < 40 = an toàn\nNội dung:\n{Content}";

    public const string Chat =
        "Trả lời ngắn gọn (tối đa {MaxSentences} câu) bằng tiếng Việt, thân thiện và chính xác. Nếu câu hỏi không liên quan đến công nghệ/IT/AI, gợi ý người dùng tìm bài viết phù hợp trong site.\nCâu hỏi: {Question}";
}
