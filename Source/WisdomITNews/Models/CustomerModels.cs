using System.ComponentModel.DataAnnotations;

namespace WisdomITNews.Models;

// ====================== ROLE KHÁCH HÀNG (suy ra, không lưu cột) ======================
// Chỉ dùng để hiển thị & lọc trong module Quản lý khách hàng.
public enum CustomerRole
{
    Trial = 0,      // Khách hàng dùng thử
    Premium = 1     // Khách hàng Premium chính thức
}

// ====================== AUDIT LOG THAO TÁC KHÁCH HÀNG ======================
// Ghi lại các thao tác đổi trạng thái / gói do Admin hoặc Nhân viên thực hiện.
public class CustomerActivityLog
{
    public int Id { get; set; }
    public int UserId { get; set; }                     // khách hàng bị tác động
    public string ActorRole { get; set; } = "";         // "Admin" / "NhanVien"
    public string ActorName { get; set; } = "";         // tên người thực hiện
    public string Action { get; set; } = "";            // LockAccount / UnlockAccount / CancelSubscription / UpdatePlan / ChangeEndDate / ChangePlan
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

// ====================== VIEWMODELS ======================
// 1 dòng trong danh sách khách hàng (User + gói hiện hành + role suy ra).
public class CustomerListItem
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public string? Phone { get; set; }
    public CustomerRole Role { get; set; }
    public int PlanId { get; set; }
    public string PlanName { get; set; } = "";
    public SubscriptionStatus SubStatus { get; set; }
    public bool AccountActive { get; set; }             // User.IsActive (false = bị khóa)
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int CurrentSubId { get; set; }
}

// Trang chi tiết khách hàng.
public class CustomerDetailVM
{
    public User User { get; set; } = null!;
    public UserSubscription? Current { get; set; }
    public string? CurrentPlanName { get; set; }
    public CustomerRole Role { get; set; }
    public List<UserSubscription> History { get; set; } = new();          // toàn bộ gói (lịch sử)
    public Dictionary<int, string> PlanNames { get; set; } = new();       // planId -> tên gói
    public List<Transaction> Transactions { get; set; } = new();
    public List<CustomerActivityLog> Logs { get; set; } = new();
    public bool HadPaidTx { get; set; }
}
