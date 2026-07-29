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
    private readonly NotificationService _notif;
    private readonly ILogger<AdContactController> _logger;

    public AdContactController(AppDbContext db, EmailService email, NotificationService notif, ILogger<AdContactController> logger)
    {
        _db = db;
        _email = email;
        _notif = notif;
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

        // Sinh Mã Số Hóa Đơn ngay khi đăng ký (hiển thị cho khách + dùng cho phiếu thu/hóa đơn)
        form.InvoiceNumber = $"HD{DateTime.Now:yyyyMMdd}-{form.Id:D4}";
        await _db.SaveChangesAsync();

        try
        {
            if (_email.IsConfigured && !string.IsNullOrWhiteSpace(form.Email))
                await _email.SendAsync(form.Email, $"[WisdomITNews] Đăng ký quảng cáo thành công — Mã HĐ {form.InvoiceNumber}", BuildEmailHtml(form, slot.Name), form.ContactName);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Gửi email đơn quảng cáo #{Id} thất bại", form.Id); }

        // Đồng thời gửi THÔNG BÁO vào hộp thư của khách
        try
        {
            var name = form.BuyerType == "company" && !string.IsNullOrWhiteSpace(form.CompanyName) ? form.CompanyName! : form.ContactName;
            var content = $"Chúc mừng {name}, bạn đã đăng ký quảng cáo thành công (mã hóa đơn {form.InvoiceNumber}). "
                + "Vui lòng liên hệ với chúng tôi để trao đổi về việc thanh toán — Hotline: (028) 3999 8888 · Email: quangcao@wisdomitnews.vn.";
            await _notif.SendToUserAsync(uid.Value, "🎉 Đăng ký quảng cáo thành công", content);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Gửi thông báo hộp thư đơn quảng cáo #{Id} thất bại", form.Id); }

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
        var name = b.BuyerType == "company" && !string.IsNullOrWhiteSpace(b.CompanyName) ? b.CompanyName! : b.ContactName;

        // Khối thông tin công ty (chỉ khi là công ty)
        var companyBlock = b.BuyerType == "company"
            ? $@"
    <tr><td colspan='2' style='padding:10px 0 4px;font-weight:700;color:#159aa3'>Thông tin công ty (dùng cho hóa đơn VAT)</td></tr>
    <tr><td style='padding:5px 0;color:#6b7280'>Tên công ty</td><td style='text-align:right;font-weight:600'>{E(b.CompanyName)}</td></tr>
    <tr><td style='padding:5px 0;color:#6b7280'>Mã số thuế</td><td style='text-align:right;font-weight:600'>{E(b.TaxCode)}</td></tr>
    <tr><td style='padding:5px 0;color:#6b7280'>Người đại diện</td><td style='text-align:right;font-weight:600'>{E(string.IsNullOrWhiteSpace(b.Representative) ? b.ContactName : b.Representative)}</td></tr>"
            + (string.IsNullOrWhiteSpace(b.Website) ? "" : $@"
    <tr><td style='padding:5px 0;color:#6b7280'>Website</td><td style='text-align:right;font-weight:600'>{E(b.Website)}</td></tr>")
            : "";

        return $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;border:1px solid #e5e7eb;border-radius:10px;overflow:hidden'>
  <div style='background:#0e7d85;color:#fff;padding:16px 20px;font-size:18px;font-weight:700'>WisdomITNews · Đăng ký quảng cáo thành công</div>
  <div style='padding:20px;color:#1f2937;font-size:14px;line-height:1.7'>
    <p>Chúc mừng <b>{E(name)}</b>, bạn đã <b>đăng ký quảng cáo thành công</b> trên Báo Online WisdomITNews.</p>
    <div style='background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;padding:12px 14px;margin:10px 0'>
      Mã số hóa đơn của bạn: <b style='color:#c2410c;font-size:16px;letter-spacing:.5px'>{E(b.InvoiceNumber)}</b>
    </div>

    <table style='width:100%;border-collapse:collapse;margin:12px 0'>
      <tr><td colspan='2' style='padding:4px 0;font-weight:700;color:#111827'>Thông tin của bạn ({(b.BuyerType == "company" ? "Công ty" : "Cá nhân")})</td></tr>
      <tr><td style='padding:5px 0;color:#6b7280'>Người liên hệ</td><td style='text-align:right;font-weight:600'>{E(b.ContactName)}</td></tr>
      <tr><td style='padding:5px 0;color:#6b7280'>Điện thoại</td><td style='text-align:right;font-weight:600'>{E(b.Phone)}</td></tr>
      <tr><td style='padding:5px 0;color:#6b7280'>Email</td><td style='text-align:right;font-weight:600'>{E(b.Email)}</td></tr>
      {(string.IsNullOrWhiteSpace(b.Address) ? "" : $"<tr><td style='padding:5px 0;color:#6b7280'>Địa chỉ</td><td style='text-align:right;font-weight:600'>{E(b.Address)}</td></tr>")}
      {companyBlock}
      <tr><td colspan='2' style='padding:10px 0 4px;font-weight:700;color:#111827;border-top:1px solid #eee'>Chi tiết đơn</td></tr>
      <tr><td style='padding:5px 0;color:#6b7280'>Vị trí</td><td style='text-align:right;font-weight:600'>{E(posLabel)}</td></tr>
      <tr><td style='padding:5px 0;color:#6b7280'>Thời lượng</td><td style='text-align:right;font-weight:600'>{b.DurationDays} ngày</td></tr>
      <tr><td style='padding:5px 0;color:#6b7280'>Tạm tính</td><td style='text-align:right;font-weight:800;color:#0e7d85'>{b.Amount:#,##0} đ</td></tr>
    </table>

    <div style='background:#f0fdfa;border:1px dashed #0e7d85;border-radius:8px;padding:14px'>
      <b>Liên hệ với chúng tôi để bàn về thanh toán</b><br/>
      Báo Online <b>WisdomITNews</b> · MST: 0312345678<br/>
      Địa chỉ: 123 Nguyễn Văn Cừ, TP. Hồ Chí Minh<br/>
      Hotline: <b>(028) 3999 8888</b> · Email: <b>quangcao@wisdomitnews.vn</b><br/>
      <span style='color:#94a3b8'>* Nội dung mô phỏng phục vụ demo dự án tốt nghiệp.</span>
    </div>
  </div>
</div>";
    }
}
