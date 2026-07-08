namespace WisdomITNews.Models;

// Hồ sơ nhân sự (1-1 với Admin). Chỉ lưu AdminId (không nav EF) cho gọn — giống JournalistProfile.
public class StaffProfile
{
    public int Id { get; set; }
    public int AdminId { get; set; }

    // ===== Cá nhân =====
    public string? Avatar { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? PlaceOfBirth { get; set; }
    public string? Nationality { get; set; }
    public string? IdNumber { get; set; }          // CCCD/CMND/Hộ chiếu
    public DateTime? IdIssueDate { get; set; }      // Ngày cấp
    public string? IdIssuePlace { get; set; }       // Nơi cấp
    public string? MaritalStatus { get; set; }      // Tình trạng hôn nhân

    // ===== Liên hệ =====
    public string? PersonalEmail { get; set; }
    public string? Phone { get; set; }
    public string? PermanentAddress { get; set; }   // Địa chỉ thường trú
    public string? CurrentAddress { get; set; }     // Địa chỉ hiện tại
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    // ===== Công việc =====
    public string? Department { get; set; }         // Phòng ban
    public string? JobTitle { get; set; }           // Chức vụ
    public string? Level { get; set; }              // Cấp bậc
    public int? ManagerId { get; set; }             // Người quản lý trực tiếp (Admin superadmin)
    public DateTime? JoinDate { get; set; }         // Ngày vào làm
    public string? ContractType { get; set; }       // Loại hợp đồng
    public string? ContractTerm { get; set; }       // Thời hạn hợp đồng
    public string? StatusNote { get; set; }         // Ghi chú trạng thái (lý do nghỉ...)
    public DateTime? ReturnDate { get; set; }       // Ngày dự kiến trở lại

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

// Nhật ký hoạt động nhân viên/admin (GĐ3). Mọi nhân viên xem, chỉ Super Admin xóa.
public class StaffActivityLog
{
    public int Id { get; set; }
    public int? ActorAdminId { get; set; }         // ai thực hiện
    public string ActorName { get; set; } = "";    // tên người thực hiện (chụp lại)
    public string ActorRole { get; set; } = "";    // superadmin / editor
    public string Action { get; set; } = "";       // mã: edit_profile / change_status / add_article / import_rss / post_notification
    public string Detail { get; set; } = "";       // mô tả chi tiết
    public int? TargetAdminId { get; set; }        // hồ sơ bị tác động (nếu có)
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
