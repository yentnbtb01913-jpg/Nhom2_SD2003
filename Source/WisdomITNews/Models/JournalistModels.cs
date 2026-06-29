namespace WisdomITNews.Models;

// Hồ sơ Nhà báo (1-1 với User có Role="Journalist"). Chỉ lưu UserId (không nav EF) cho gọn.
public class JournalistProfile
{
    public int Id { get; set; }
    public int UserId { get; set; }

    // Cơ bản
    public string? PenName { get; set; }            // Bút danh
    public string? Gender { get; set; }             // Giới tính (tùy chọn)
    public DateTime? DateOfBirth { get; set; }      // Ngày sinh
    public string? Nationality { get; set; }        // Quốc tịch
    public string? Address { get; set; }            // Địa chỉ
    public string? City { get; set; }               // Thành phố
    public string? Country { get; set; }            // Quốc gia

    // Liên hệ
    public string? Phone { get; set; }
    public string? Facebook { get; set; }
    public string? LinkedIn { get; set; }
    public string? Twitter { get; set; }            // X
    public string? Website { get; set; }
    public string? Zalo { get; set; }
    public string? Telegram { get; set; }

    // Nghề nghiệp
    public string? JobTitle { get; set; }           // Chức danh
    public string? Organization { get; set; }       // Cơ quan/Báo
    public string? AssignedCategory { get; set; }   // Chuyên mục phụ trách
    public int? YearsExperience { get; set; }       // Số năm kinh nghiệm
    public string? PressCardNo { get; set; }        // Thẻ nhà báo
    public DateTime? PressCardIssued { get; set; }  // Ngày cấp
    public DateTime? PressCardExpiry { get; set; }  // Ngày hết hạn

    // Chuyên môn + nội bộ
    public string? Expertise { get; set; }          // Chuyên môn (tag, phân tách dấu phẩy)
    public string? InternalNote { get; set; }       // Ghi chú nội bộ (không hiển thị ngoài)

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
