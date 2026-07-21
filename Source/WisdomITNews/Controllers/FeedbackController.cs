using Microsoft.AspNetCore.Mvc;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Controllers;

/// <summary>
/// [M] Báo cáo lỗi / góp ý từ người đọc.
/// </summary>
public class FeedbackController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<FeedbackController> _logger;

    public FeedbackController(AppDbContext db, ILogger<FeedbackController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Đây là luồng xử lý gửi góp ý / báo lỗi
    // Luồng: 1) Kiểm tra có mô tả (Description) không -> thiếu thì trả 400
    //        2) Tự lấy URL trang đang xem từ header Referer (fallback req.PageUrl)
    //        3) Tạo FeedbackReport {PageUrl, Type (mặc định "other"), Description, IsResolved=false}
    //        4) Lưu DB -> trả JSON cảm ơn; lỗi -> ghi log + trả 500
    // Bảng: FeedbackReports  (xử lý duyệt tại /admin/Feedbacks)
    [HttpPost]
    [Route("/gop-y")]
    public async Task<IActionResult> Report([FromBody] FeedbackRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Description))
            return BadRequest(new { success = false, message = "Vui lòng nhập mô tả" });

        try
        {
            // Tự lấy URL từ Referer
            var pageUrl = Request.Headers["Referer"].ToString();
            if (string.IsNullOrEmpty(pageUrl)) pageUrl = req.PageUrl ?? "";

            var fb = new FeedbackReport
            {
                PageUrl     = pageUrl,
                Type        = string.IsNullOrWhiteSpace(req.Type) ? "other" : req.Type.Trim(),
                Description = req.Description.Trim(),
                CreatedAt   = DateTime.Now,
                IsResolved  = false
            };
            _db.FeedbackReports.Add(fb);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Cảm ơn bạn đã góp ý!" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Feedback.Report failed");
            return StatusCode(500, new { success = false, message = "Không gửi được, thử lại" });
        }
    }
}

public class FeedbackRequest
{
    public string? Type { get; set; }
    public string Description { get; set; } = "";
    public string? PageUrl { get; set; }
}
