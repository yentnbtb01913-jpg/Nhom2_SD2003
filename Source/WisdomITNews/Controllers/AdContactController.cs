using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;
using WisdomITNews.Services;

namespace WisdomITNews.Controllers;

// Trang LIÊN HỆ QUẢNG CÁO (Báo Online) cho NGƯỜI DÙNG thường (Session "UserId"):
//  /lien-he-quang-cao            -> trang giới thiệu, 1 thẻ "Báo Online"
//  /lien-he-quang-cao/dang-ky    -> BẮT đăng nhập; form thông tin cá nhân/công ty
//  /lien-he-quang-cao/xac-nhan   -> xem lại thông tin + tạm tính
//  /lien-he-quang-cao/dat        -> lưu đơn (AdBooking) + gửi email thanh toán (demo)
//  /lien-he-quang-cao/hoan-tat/{id}
// (Khác với AdvertiseController ở /quang-cao vốn dành cho Nhà báo/Admin với AdSlots + banner.)
public class AdContactController : Controller
{
    private readonly AppDbContext _db;
    private readonly EmailService _email;
    private readonly ILogger<AdContactController> _logger;

    public AdContactController(AppDbContext db, EmailService email, ILogger<AdContactController> logger)
    {
        _db = db;
        _email = email;
        _logger = logger;
    }

    private int? CurrentUserId => HttpContext.Session.GetInt32("UserId");
    private string LoginTo(string returnPath) => "/Account/Login?returnUrl=" + Uri.EscapeDataString(returnPath);

    // Tất cả slot quảng cáo đang nhận đặt (đọc từ DB — quản lý ở "Slot QC").
    private async Task<List<AdSlot>> ActiveSlotsAsync()
        => await _db.AdSlots.Where(s => s.IsActive)
                            .OrderByDescending(s => s.PricePerDay).ThenBy(s => s.Id)
                            .ToListAsync();

    [Route("/lien-he-quang-cao")]
    public IActionResult Index() => View();

    // Sau khi đăng nhập: xem BẢNG GIÁ / TẤT CẢ vị trí quảng cáo trước khi đăng ký
    [Route("/lien-he-quang-cao/bang-gia")]
    public async Task<IActionResult> Pricing()
    {
        if (CurrentUserId == null) return Redirect(LoginTo("/lien-he-quang-cao/bang-gia"));
        ViewBag.Slots = await ActiveSlotsAsync();
        return View();
    }

    [Route("/lien-he-quang-cao/dang-ky")]
    public async Task<IActionResult> Register(string? pos = null)
    {
        if (CurrentUserId == null) return Redirect(LoginTo("/lien-he-quang-cao/dang-ky"));

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId.Value);
        var slots = await ActiveSlotsAsync();
        ViewBag.Slots = slots;
        return View(new AdBooking
        {
            ContactName = user?.FullName ?? "",
            Email = user?.Email ?? "",
            Phone = user?.Phone ?? "",
            BuyerType = "individual",
            DurationDays = 7,
            AdPosition = (pos != null && slots.Any(s => s.SlotKey == pos)) ? pos : (slots.FirstOrDefault()?.SlotKey ?? "")
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("/lien-he-quang-cao/xac-nhan")]
    public async Task<IActionResult> Confirm(AdBooking form)
    {
        if (CurrentUserId == null) return Redirect(LoginTo("/lien-he-quang-cao/dang-ky"));
        var slots = await ActiveSlotsAsync();
        ViewBag.Slots = slots;
        NormalizeDuration(form);

        var slot = slots.FirstOrDefault(s => s.SlotKey == form.AdPosition);
        var missing = slot == null || string.IsNullOrWhiteSpace(form.ContactName) || string.IsNullOrWhiteSpace(form.Email)
            || string.IsNullOrWhiteSpace(form.Phone)
            || (form.BuyerType == "company" && string.IsNullOrWhiteSpace(form.CompanyName));
        if (missing)
        {
            ViewBag.Error = "Vui lòng điền đủ thông tin bắt buộc (*) và chọn vị trí quảng cáo.";
            return View("Register", form);
        }

        form.Amount = slot!.PricePerDay * form.DurationDays;
        return View("Confirm", form);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("/lien-he-quang-cao/dat")]
    public async Task<IActionResult> Place(AdBooking form)
    {
        var uid = CurrentUserId;
        if (uid == null) return Redirect(LoginTo("/lien-he-quang-cao/dang-ky"));
        var slots = await ActiveSlotsAsync();
        NormalizeDuration(form);
        var slot = slots.FirstOrDefault(s => s.SlotKey == form.AdPosition) ?? slots.FirstOrDefault();
        if (slot == null) return Redirect("/lien-he-quang-cao/bang-gia");

        form.Id = 0;
        form.UserId = uid.Value;
        form.AdPosition = slot.SlotKey;
        form.Amount = slot.PricePerDay * form.DurationDays;
        form.Status = "PendingConfirmation";
        form.CreatedAt = DateTime.Now;
        if (form.BuyerType != "company") { form.CompanyName = null; form.TaxCode = null; form.Website = null; }

        _db.AdBookings.Add(form);
        await _db.SaveChangesAsync();

        try
        {
            if (_email.IsConfigured && !string.IsNullOrWhiteSpace(form.Email))
                await _email.SendAsync(form.Email, $"[WisdomITNews] Xác nhận đơn quảng cáo #{form.Id}", BuildEmailHtml(form, slot.Name), form.ContactName);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Gửi email đơn quảng cáo #{Id} thất bại", form.Id); }

        return RedirectToAction(nameof(Done), new { id = form.Id });
    }

    [Route("/lien-he-quang-cao/hoan-tat/{id:int}")]
    public async Task<IActionResult> Done(int id)
    {
        var booking = await _db.AdBookings.FirstOrDefaultAsync(b => b.Id == id && b.UserId == CurrentUserId);
        if (booking == null) return Redirect("/lien-he-quang-cao");
        var slot = await _db.AdSlots.FirstOrDefaultAsync(s => s.SlotKey == booking.AdPosition);
        ViewBag.PositionLabel = slot?.Name ?? booking.AdPosition;
        ViewBag.EmailConfigured = _email.IsConfigured;
        return View(booking);
    }

    private static void NormalizeDuration(AdBooking f)
    {
        if (f.BuyerType != "company") f.BuyerType = "individual";
        if (f.DurationDays < 1) f.DurationDays = 1;
        if (f.DurationDays > 365) f.DurationDays = 365;
    }

    private string BuildEmailHtml(AdBooking b, string posLabel)
    {
        string E(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "-");
        var who = b.BuyerType == "company"
            ? $"Công ty: <b>{E(b.CompanyName)}</b> (MST: {E(b.TaxCode)})<br/>Người liên hệ: {E(b.ContactName)}"
            : $"Người đặt: <b>{E(b.ContactName)}</b>";
        return $@"
<div style='font-family:Arial,sans-serif;max-width:560px;margin:auto;border:1px solid #e5e7eb;border-radius:10px;overflow:hidden'>
  <div style='background:#0e7d85;color:#fff;padding:16px 20px;font-size:18px;font-weight:700'>WisdomITNews · Xác nhận đơn quảng cáo</div>
  <div style='padding:20px;color:#1f2937;font-size:14px;line-height:1.7'>
    <p>Cảm ơn bạn đã đăng ký quảng cáo trên <b>Báo Online WisdomITNews</b>. Đơn <b>#{b.Id}</b> đã được ghi nhận.</p>
    <p>{who}<br/>Email: {E(b.Email)} · ĐT: {E(b.Phone)}</p>
    <table style='width:100%;border-collapse:collapse;margin:12px 0'>
      <tr><td style='padding:6px 0;color:#6b7280'>Vị trí</td><td style='text-align:right;font-weight:600'>{E(posLabel)}</td></tr>
      <tr><td style='padding:6px 0;color:#6b7280'>Thời lượng</td><td style='text-align:right;font-weight:600'>{b.DurationDays} ngày</td></tr>
      <tr><td style='padding:6px 0;color:#6b7280;border-top:1px solid #eee'>Tạm tính</td><td style='text-align:right;font-weight:800;color:#0e7d85;border-top:1px solid #eee'>{b.Amount:#,##0} đ</td></tr>
    </table>
    <div style='background:#f0fdfa;border:1px dashed #0e7d85;border-radius:8px;padding:14px'>
      <b>Hướng dẫn thanh toán (DEMO)</b><br/>
      Ngân hàng: <b>Vietcombank</b> — STK: <b>0123 456 789</b><br/>
      Chủ TK: <b>WISDOM IT NEWS</b> · Nội dung CK: <b>QC {b.Id}</b><br/>
      <span style='color:#94a3b8'>* Thông tin thanh toán mô phỏng cho mục đích demo dự án tốt nghiệp.</span>
    </div>
  </div>
</div>";
    }
}
