namespace WisdomITNews.Models;

// Mục đích của token xác nhận email.
public enum EmailTokenPurpose
{
    Registration = 0,        // xác nhận đăng ký tài khoản
    SubscriptionReceipt = 1, // (giữ tương thích) biên nhận mua gói - không dùng ở luồng mới
    TrialActivation = 2,     // kích hoạt dùng thử Premium qua email
    PurchaseConfirmation = 3 // xác nhận thanh toán mua gói Premium (giả lập qua email)
}

// Token xác nhận email — dùng chung cho đăng ký & biên nhận Premium.
public class EmailConfirmationToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Token { get; set; } = "";                 // ngẫu nhiên an toàn, unique
    public EmailTokenPurpose Purpose { get; set; } = EmailTokenPurpose.Registration;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ExpiresAt { get; set; } = DateTime.Now.AddHours(48);
    public DateTime? ConfirmedAt { get; set; }              // null = chưa xác nhận/xem

    // Liên kết nghiệp vụ (cột nullable, không ràng buộc FK cứng - theo convention hiện có)
    public int? SubscriptionId { get; set; }   // token TrialActivation -> UserSubscription
    public int? TransactionId { get; set; }    // token PurchaseConfirmation -> Transaction

    public User? User { get; set; }
}
