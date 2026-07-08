using System.ComponentModel.DataAnnotations;

namespace WisdomITNews.Models;

// ====================== ENUM ======================
public enum SubscriptionStatus
{
    Trial = 0,      // đang dùng thử (đã kích hoạt qua email)
    Active = 1,     // đã mua, còn hiệu lực
    Expired = 2,    // hết hạn
    Cancelled = 3   // đã hủy (tay hoặc auto do trial không xác nhận)
}

public enum TransactionStatus
{
    Pending = 0,    // vừa tạo, chờ xác nhận qua email
    Success = 1,    // đã xác nhận thanh toán
    Failed = 2,     // thất bại / hết hạn xác nhận
    Cancelled = 3   // hủy bởi người dùng
}

// ====================== GÓI ======================
public class SubscriptionPlan
{
    public int Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int TrialDays { get; set; } = 0;      // 0 = không có dùng thử
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public ICollection<PlanFeature> Features { get; set; } = new List<PlanFeature>();
}

public class PlanFeature
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    [Required] public string FeatureText { get; set; } = "";
    public int SortOrder { get; set; } = 0;

    public SubscriptionPlan? Plan { get; set; }
}

// ====================== ĐĂNG KÝ CỦA NGƯỜI DÙNG ======================
public class UserSubscription
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PlanId { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trial;
    public DateTime StartDate { get; set; } = DateTime.Now;
    public DateTime EndDate { get; set; }
    public DateTime? ConfirmedAt { get; set; }          // null = trial chưa xác nhận qua email
    public string? Notes { get; set; }                  // ghi chú (hủy tay / auto-cancel...)
    public DateTime? ExpiryReminderSentAt { get; set; } // đã gửi mail nhắc hết hạn (1 lần)
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public SubscriptionPlan? Plan { get; set; }
}

// ====================== GIAO DỊCH (giả lập) ======================
public class Transaction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PlanId { get; set; }
    public int? UserSubscriptionId { get; set; }        // null nếu giao dịch chưa tạo được sub
    public decimal Amount { get; set; }
    public string PaymentMethodLabel { get; set; } = ""; // chỉ để hiển thị, VD "VNPay (giả lập)"
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public SubscriptionPlan? Plan { get; set; }
}


// Một dòng đối chiếu doanh thu (kỳ hiện tại vs kỳ trước) — dùng cho Dashboard doanh thu.
public class RevenueCompareRow
{
    public string Key { get; set; } = "";
    public string CurLabel { get; set; } = "";
    public string PrevLabel { get; set; } = "";
    public decimal Current { get; set; }
    public decimal Previous { get; set; }
    public int CurCount { get; set; }
    public int PrevCount { get; set; }
}
