namespace WisdomITNews.Models;

// Cấu hình chức năng Tự động nhập bài (Auto Import). Chỉ 1 hàng (Id = 1).
public class AutoImportSettings
{
    public int Id { get; set; } = 1;

    public bool Enabled { get; set; } = false;              // #1 bật/tắt tổng
    public int ScanIntervalSeconds { get; set; } = 60;      // #2 chu kỳ quét (giây)
    public int MaxPerSource { get; set; } = 20;             // #3 số bài tối đa mỗi nguồn / lần
    public int DelayBetweenArticlesSeconds { get; set; } = 0;  // #4 nghỉ giữa mỗi bài (giây)
    public int DelayBetweenSourcesSeconds { get; set; } = 2;   // #5 nghỉ giữa mỗi nguồn (giây)
    public int Concurrency { get; set; } = 1;               // #6 số nguồn xử lý đồng thời
    public int MaxTotalPerRun { get; set; } = 100;          // #7 tổng số bài tối đa mỗi lần chạy
    public int RetrySeconds { get; set; } = 300;            // #8 thử lại khi nguồn lỗi (giây)
    public bool OnlyNew { get; set; } = true;               // #9 chỉ nhập bài mới

    // #10 bật/tắt ghi log theo từng loại sự kiện
    public bool LogSuccess { get; set; } = true;
    public bool LogSkipDuplicate { get; set; } = true;
    public bool LogError { get; set; } = true;
    public bool LogConnectionError { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    // Domain Admin: admin chỉnh cấu hình auto-import gần nhất (audit). NULL = chưa rõ/seed.
    public int? UpdatedByAdminId { get; set; }
}
