using System.ComponentModel.DataAnnotations;

namespace WisdomITNews.Models;

// ====================== [ĐÃ GỠ PREMIUM] ======================
// SubscriptionPlan / PlanFeature / UserSubscription / SubscriptionStatus đã được loại bỏ.
// Giữ lại Transaction (tái dùng làm ĐƠN THANH TOÁN QUẢNG CÁO) + TransactionStatus + RevenueCompareRow.

public enum TransactionStatus
{
    Pending = 0,    // vừa tạo, chờ thanh toán
    Success = 1,    // đã thanh toán
    Failed = 2,     // thất bại / hết hạn
    Cancelled = 3   // hủy
}

// ====================== GIAO DỊCH (đơn thanh toán quảng cáo) ======================
public class Transaction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? AdvertisementId { get; set; }            // đơn thanh toán cho quảng cáo nào
    public decimal Amount { get; set; }
    public string PaymentMethodLabel { get; set; } = ""; // VD "Chuyển khoản ngân hàng"
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public Advertisement? Advertisement { get; set; }
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
