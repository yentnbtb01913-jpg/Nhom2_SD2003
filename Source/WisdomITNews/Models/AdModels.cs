namespace WisdomITNews.Models;

// Quảng cáo banner theo vị trí (header / sidebar / in_article), có lịch chạy + đếm hiển thị/click.
public class Advertisement
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string TargetUrl { get; set; } = "";           // link đích khi click
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
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Đang hiệu lực để hiển thị?
    public bool IsLive(DateTime now) =>
        IsActive && Status == "approved"
        && (StartDate == null || StartDate <= now)
        && (EndDate == null || EndDate >= now);
}


// Tin nhắn chat nội bộ giữa Nhà báo (chủ QC) và Quản trị (admin/nhân viên) để gia hạn QC.
public class AdRenewalMessage
{
    public int Id { get; set; }
    public int AdvertisementId { get; set; }
    public string SenderRole { get; set; } = "";   // journalist / admin / nhanvien
    public int SenderId { get; set; }
    public string SenderName { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsReadByAdmin { get; set; } = false;
    public bool IsReadByJournalist { get; set; } = false;

    public Advertisement? Advertisement { get; set; }
}
