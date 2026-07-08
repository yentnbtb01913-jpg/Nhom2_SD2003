namespace WisdomITNews.Models;

// Ánh xạ nhãn AI/nguồn nhận diện -> danh mục hệ thống.
// Khi AI trả về AiLabel đã được ánh xạ, hệ thống tự chuyển sang CategoryId tương ứng.
public class CategoryMapping
{
    public int Id { get; set; }
    public string AiLabel { get; set; } = "";     // nhãn AI/RSS nhận diện (vd: "Công nghệ", "Security")
    public int CategoryId { get; set; }           // danh mục hệ thống thật
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Category? Category { get; set; }
}

// Nhật ký admin chỉnh sửa kết quả phân loại danh mục của AI.
public class AiCategoryCorrectionLog
{
    public int Id { get; set; }
    public int? ArticleId { get; set; }
    public string EditorName { get; set; } = "";  // người chỉnh sửa
    public string OldCategory { get; set; } = ""; // danh mục AI ban đầu
    public string NewCategory { get; set; } = ""; // danh mục sau khi sửa
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
