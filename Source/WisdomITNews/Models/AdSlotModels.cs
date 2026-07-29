using System.ComponentModel.DataAnnotations;

namespace WisdomITNews.Models;

// Slot quảng cáo bán được — định nghĩa vị trí + kích thước + giá/ngày.
// Người mua sẽ đặt Advertisement (đơn QC) gắn vào một AdSlot.
public class AdSlot
{
    public int Id { get; set; }
    [Required] public string Name { get; set; } = "";        // VD: "Banner Đầu Trang"
    [Required] public string SlotKey { get; set; } = "";     // = mã khu render: header/home_left/home_right/in_article/sidebar
    public string? Description { get; set; }                  // mô tả hiển thị
    public string Size { get; set; } = "";                    // VD: "Banner728x90"
    public decimal PricePerDay { get; set; }                  // giá mỗi ngày
    public bool IsActive { get; set; } = true;                // còn nhận đặt hay không
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

// Cấu hình mỗi khu hiển thị (zone) — chu kỳ xoay vòng do Admin/NhanVien chỉnh.
// Position trùng với SlotKey/zone: header / home_left / home_right / in_article / sidebar.
public class AdZoneSetting
{
    public int Id { get; set; }
    [Required] public string Position { get; set; } = "";     // mã khu (= zone/SlotKey)
    public int RotationSeconds { get; set; } = 5;             // chu kỳ nhảy QC (giây)
}

// View-model cho AdSlot ViewComponent: danh sách QC của khu + chu kỳ nhảy.
public class AdZoneRender
{
    public List<Advertisement> Ads { get; set; } = new();
    public int RotationMs { get; set; } = 5000;
    public string? SlotSize { get; set; }   // kích thước slot của KHU (vd "Billboard970x250") -> dùng cố định chiều cao ô
}

// ===== Bảng điều khiển bố cục quảng cáo (preview + sắp thứ tự) =====
// 1 khu = 1 AdSlot (SlotKey = zone). Chứa toàn bộ QC gắn khu đó (mọi trạng thái) để xem trước.
public class AdLayoutZoneVm
{
    public string Position { get; set; } = "";       // = SlotKey/zone
    public string Name { get; set; } = "";
    public string Size { get; set; } = "";
    public int RotationSeconds { get; set; } = 5;
    public List<Advertisement> Ads { get; set; } = new();
}

// DTO nhận khi bấm Lưu (AJAX): thứ tự QC + chu kỳ mỗi khu.
public class AdLayoutSaveDto
{
    public List<AdLayoutZoneDto> Zones { get; set; } = new();
}
public class AdLayoutZoneDto
{
    public string Position { get; set; } = "";
    public int RotationSeconds { get; set; } = 5;
    public List<int> AdIds { get; set; } = new();     // thứ tự QC trong khu (index = DisplayOrder)
}

// VM trang "Quảng cáo của tôi" (Journalist Panel): preview khu (QC của tôi đang chạy) + đơn hàng.
public class JournalistAdsVm
{
    public List<AdLayoutZoneVm> Zones { get; set; } = new();   // chỉ chứa QC đang chạy của nhà báo
    public List<Advertisement> Orders { get; set; } = new();   // toàn bộ đơn QC của nhà báo
}
