using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;
using WisdomITNews.Services;

namespace WisdomITNews.Controllers;

// Khu quảng cáo (trang riêng, layout _AdLayout): xem bảng giá slot -> đặt mua -> chuyển khoản -> đơn của tôi.
// ĐĂNG quảng cáo chỉ dành cho Nhà báo / Admin / Nhân viên. Guest/User chỉ xem bảng giá + liên hệ.
public class AdvertiseController : Controller
{
    private readonly AppDbContext _db;
    private readonly ImageUploadService _imageUpload;
    private readonly EmailService _email;
    public AdvertiseController(AppDbContext db, ImageUploadService imageUpload, EmailService email)
    {
        _db = db;
        _imageUpload = imageUpload;
        _email = email;
    }

    // Người được phép đăng QC: Nhà báo (JournalistId) hoặc Admin/Nhân viên (AdminId).
    private (bool ok, int? userId, int? adminId, string name) Buyer()
    {
        var jid = HttpContext.Session.GetInt32("JournalistId");
        if (jid != null)
            return (true, jid, null, HttpContext.Session.GetString("JournalistName") ?? "Nhà báo");
        if (int.TryParse(HttpContext.Session.GetString("AdminId"), out var aid) && aid > 0)
            return (true, null, aid, HttpContext.Session.GetString("AdminName") ?? "Quản trị");
        return (false, null, null, "");
    }
    private bool CanPost => Buyer().ok;

    // Đây là luồng xử lý hiển thị bảng giá slot quảng cáo (ai cũng xem được)
    [Route("/quang-cao")]
    [Route("/Advertise")]
    public async Task<IActionResult> Index()
    {
        ViewBag.CanPost = CanPost;
        var slots = await _db.AdSlots.Where(s => s.IsActive).OrderBy(s => s.Id).ToListAsync();
        return View(slots);
    }

    // Đây là luồng xử lý hiển thị form đặt mua 1 slot (chỉ Nhà báo/Admin/Nhân viên)
    public async Task<IActionResult> Order(int slotId)
    {
        if (!CanPost)
            return RedirectToAction("Login", "Journalist", new { returnUrl = $"/Advertise/Order?slotId={slotId}" });
        var slot = await _db.AdSlots.FirstOrDefaultAsync(s => s.Id == slotId && s.IsActive);
        if (slot == null) { TempData["AdErr"] = "Slot không tồn tại."; return RedirectToAction("Index"); }
        ViewBag.Slot = slot;
        return View();
    }

    // Đây là luồng xử lý tạo đơn đặt quảng cáo
    // Luồng: validate + upload banner -> tạo Advertisement (đơn, pending, chưa TT, Amount=giá*ngày)
    //        -> tạo Transaction (Pending) -> sang trang xác nhận chuyển khoản
    // Bảng: Advertisements, Transactions, AdSlots
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Order(int slotId, string title, string targetUrl, string? phone, int days, IFormFile? bannerFile)
    {
        var buyer = Buyer();
        if (!buyer.ok) return RedirectToAction("Login", "Journalist");
        var slot = await _db.AdSlots.FirstOrDefaultAsync(s => s.Id == slotId && s.IsActive);
        if (slot == null) { TempData["AdErr"] = "Slot không tồn tại."; return RedirectToAction("Index"); }
        ViewBag.Slot = slot;

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(targetUrl) || days < 1)
        { ViewBag.Error = "Vui lòng nhập tiêu đề, link đích và số ngày (≥ 1)."; return View(); }

        string? imageUrl = null;
        if (bannerFile != null && bannerFile.Length > 0)
        {
            var up = await _imageUpload.SaveAsync(bannerFile);
            if (!up.Success) { ViewBag.Error = up.Error ?? "Lỗi upload banner."; return View(); }
            imageUrl = up.RelativePath;
        }

        var now = DateTime.Now;
        var amount = slot.PricePerDay * days;
        // GIẢ LẬP THANH TOÁN: đặt xong là tự ghi "đã thanh toán", đơn chỉ còn chờ Admin/NhanVien DUYỆT nội dung.
        var ad = new Advertisement
        {
            Title = title.Trim(),
            ImageUrl = imageUrl,
            TargetUrl = targetUrl.Trim(),
            Position = slot.SlotKey,
            AdSlotId = slot.Id,
            Days = days,
            Amount = amount,
            StartDate = now,
            EndDate = now.AddDays(days),
            IsActive = false,
            Status = "pending",            // chờ duyệt nội dung
            PaymentStatus = "paid",        // đã thanh toán (giả lập)
            CreatedByUserId = buyer.userId,
            CreatedByAdminId = buyer.adminId,
            CreatedByName = buyer.name,
            BuyerPhone = phone,
            CreatedAt = now
        };
        _db.Advertisements.Add(ad);
        await _db.SaveChangesAsync();

        _db.Transactions.Add(new Transaction
        {
            UserId = buyer.userId ?? 0,
            AdvertisementId = ad.Id,
            Amount = amount,
            PaymentMethodLabel = "Thanh toán online (giả lập)",
            Status = TransactionStatus.Success,   // thanh toán thành công ngay
            CreatedAt = now,
            UpdatedAt = now
        });
        await _db.SaveChangesAsync();

        // Gửi mail biên nhận ngay sau khi thanh toán (giả lập)
        var (emailSent, emailMsg) = await SendReceiptAsync(ad);
        TempData["AdEmail"] = emailSent ? "Đã gửi biên nhận qua email." : ("Không gửi được email biên nhận: " + emailMsg);

        return RedirectToAction("Confirm", new { id = ad.Id });
    }

    // Gửi email biên nhận thanh toán cho người mua. Trả (đã gửi?, lý do).
    // Bảng: Users, Admins
    private async Task<(bool sent, string msg)> SendReceiptAsync(Advertisement ad)
    {
        string? email = null, name = ad.CreatedByName;
        if (ad.CreatedByUserId != null)
        {
            var u = await _db.Users.FindAsync(ad.CreatedByUserId.Value);
            if (u != null) { email = u.Email; name = u.FullName; }
        }
        else if (ad.CreatedByAdminId != null)
        {
            var a2 = await _db.Admins.FindAsync(ad.CreatedByAdminId.Value);
            if (a2 != null) { email = a2.Email; name = a2.FullName; }
        }
        if (string.IsNullOrWhiteSpace(email)) return (false, "Không tìm thấy email người mua.");
        if (!_email.IsConfigured) return (false, "SMTP chưa cấu hình.");

        var body = $@"<div style='font-family:Arial,sans-serif;max-width:560px;margin:auto;'>
<h2 style='color:#0e7d85;'>Đã nhận thanh toán quảng cáo</h2>
<p>Xin chào <b>{name}</b>,</p>
<p>Chúng tôi đã <b>nhận thanh toán</b> cho đơn quảng cáo <b>QC{ad.Id}</b> — vị trí {ad.AdSlot?.Name ?? ad.Position} ({ad.Position}), {ad.Days} ngày, số tiền <b>{ad.Amount:#,##0} đ</b>, chạy {ad.StartDate:dd/MM/yyyy} → {ad.EndDate:dd/MM/yyyy}.</p>
<p style='color:#475569;'>Đơn đang <b>chờ ban biên tập duyệt nội dung</b>. Quảng cáo sẽ tự lên sóng sau khi được duyệt. Cảm ơn bạn đã tin dùng Wisdom IT News.</p></div>";
        var (ok, err) = await _email.SendAsync(email!, $"Biên nhận thanh toán quảng cáo QC{ad.Id} — Wisdom IT News", body, name);
        return ok ? (true, "") : (false, err ?? "lỗi không rõ");
    }

    // Đây là luồng xử lý trang xác nhận đơn + thông tin chuyển khoản
    public async Task<IActionResult> Confirm(int id)
    {
        var buyer = Buyer();
        if (!buyer.ok) return RedirectToAction("Login", "Journalist");
        var ad = await _db.Advertisements.Include(a => a.AdSlot)
            .FirstOrDefaultAsync(a => a.Id == id
                && ((buyer.userId != null && a.CreatedByUserId == buyer.userId)
                 || (buyer.adminId != null && a.CreatedByAdminId == buyer.adminId)));
        if (ad == null) return RedirectToAction("Index");
        // Nhà báo -> về trang "Quảng cáo của tôi" trong Journalist Panel; Admin/NhanVien -> đơn ở khu QC.
        ViewBag.MyOrdersUrl = buyer.userId != null ? "/journalist/Ads" : "/Advertise/MyOrders";
        return View(ad);
    }

    // Đây là luồng xử lý danh sách đơn quảng cáo của tôi
    public async Task<IActionResult> MyOrders()
    {
        var buyer = Buyer();
        if (!buyer.ok)
            return RedirectToAction("Login", "Journalist", new { returnUrl = "/Advertise/MyOrders" });
        var orders = await _db.Advertisements.Include(a => a.AdSlot)
            .Where(a => a.AdSlotId != null
                && ((buyer.userId != null && a.CreatedByUserId == buyer.userId)
                 || (buyer.adminId != null && a.CreatedByAdminId == buyer.adminId)))
            .OrderByDescending(a => a.Id).ToListAsync();
        return View(orders);
    }
}
