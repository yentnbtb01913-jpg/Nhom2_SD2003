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

// Đơn ĐĂNG KÝ QUẢNG CÁO trên báo online (cá nhân hoặc công ty).
// Người dùng phải đăng nhập, điền thông tin liên hệ + chi tiết vị trí/thời lượng, xác nhận rồi đặt.
public class AdBooking
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string BuyerType { get; set; } = "individual";   // individual | company

    // Người liên hệ
    public string ContactName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Address { get; set; }

    // Công ty (khi BuyerType = company)
    public string? CompanyName { get; set; }
    public string? TaxCode { get; set; }
    public string? Website { get; set; }

    // Chi tiết quảng cáo
    public string AdPosition { get; set; } = "";            // topbanner | sidebar | in_article
    public int DurationDays { get; set; } = 7;
    public decimal Amount { get; set; }                     // tạm tính (demo)
    public string? Note { get; set; }

    // Trạng thái theo quy trình: PendingConfirmation | AwaitingPayment | AwaitingContent
    //                            | UnderReview | Scheduled | Live | Completed | Rejected
    public string Status { get; set; } = "PendingConfirmation";
    public string? AdminNote { get; set; }                   // ghi chú NỘI BỘ của admin (đã nhận tiền, khách gửi banner…)
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public User? User { get; set; }
}
