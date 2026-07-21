namespace WisdomITNews.Models;

// Bài báo người dùng đã lưu để đọc sau (khu cá nhân -> "Báo đã lưu")
public class SavedArticle
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ArticleId { get; set; }
    public DateTime SavedAt { get; set; } = DateTime.Now;

    // Domain User / Article
    public User? User { get; set; }
    public Article? Article { get; set; }
}

// Người dùng theo dõi một chuyên mục (khu cá nhân -> "Mục của bạn")
public class UserCategoryFollow
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CategoryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Domain User / Category
    public User? User { get; set; }
    public Category? Category { get; set; }
}
