namespace WisdomITNews.Models;

// Quảng cáo banner theo vị trí (header / sidebar / in_article), có lịch chạy + đếm hiển thị/click.
public class Advertisement
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    // Loại sáng tạo: "image" = banner ảnh (ImageUrl+TargetUrl); "html" = mã HTML/JS (HtmlContent, nhúng iframe sandbox).
    public string AdType { get; set; } = "image";
    public string? ImageUrl { get; set; }
    public string? HtmlContent { get; set; }              // mã HTML/JS khách gửi (chạy trong iframe cách ly)
    public string TargetUrl { get; set; } = "";           // link đích khi click (dùng cho loại ảnh)
    public string Position { get; set; } = "sidebar";     // header / sidebar / in_article
    public DateTime? StartDate { get; set; }              // lịch chạy (null = không giới hạn)
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = "approved";      // pending / approved / rejected
    public int Impressions { get; set; } = 0;             // lượt hiển thị
    public int Clicks { get; set; } = 0;                  // lượt click
    public int? CreatedByUserId { get; set; }             // nhà báo (User) tạo
    public int? CreatedByAdminId { get; set; }            // admin tạo
    public string CreatedByName { get; set; } = "";
    public int DisplayOrder { get; set; } = 0;            // thứ tự xoay vòng trong 1 khu (nhỏ chạy trước)
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // ===== Đơn đặt quảng cáo (Bán quảng cáo) =====
    public int? AdSlotId { get; set; }                    // slot đã đặt
    public int Days { get; set; } = 0;                    // số ngày thuê
    public decimal Amount { get; set; } = 0;              // tổng tiền = PricePerDay * Days
    public string? BuyerPhone { get; set; }               // SĐT liên hệ người mua
    public string PaymentStatus { get; set; } = "unpaid"; // unpaid / paid

    // ===== Xóa mềm (thùng rác) — xóa không mất hẳn, có thể khôi phục =====
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    public AdSlot? AdSlot { get; set; }

    // Đang hiệu lực để hiển thị?
    public bool IsLive(DateTime now) =>
        !IsDeleted && IsActive && Status == "approved"
        && (StartDate == null || StartDate <= now)
        && (EndDate == null || EndDate >= now);
}


// [ĐÃ GỠ] Model AdRenewalMessage (chat gia hạn quảng cáo) đã được loại bỏ.
