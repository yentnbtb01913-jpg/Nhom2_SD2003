using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;

namespace WisdomITNews.Services;

// Suy ra gói hiện hành + role khách hàng từ danh sách UserSubscription.
public static class CustomerHelper
{
    // Chọn subscription "hiện hành" của 1 khách hàng theo thứ tự ưu tiên.
    public static UserSubscription? PickCurrent(IEnumerable<UserSubscription> subs)
    {
        var list = subs as IList<UserSubscription> ?? subs.ToList();
        var now = DateTime.Now;

        // 1) Premium đang hiệu lực (đã xác nhận, còn hạn)
        var active = list
            .Where(s => s.Status == SubscriptionStatus.Active && s.ConfirmedAt != null && s.EndDate > now)
            .OrderByDescending(s => s.EndDate).FirstOrDefault();
        if (active != null) return active;

        // 2) Trial đang chạy (còn hạn)
        var trial = list
            .Where(s => s.Status == SubscriptionStatus.Trial && s.EndDate > now)
            .OrderByDescending(s => s.EndDate).FirstOrDefault();
        if (trial != null) return trial;

        // 3) Còn lại: bản ghi mới nhất (Expired / Cancelled / hết hạn)
        return list.OrderByDescending(s => s.CreatedAt).FirstOrDefault();
    }

    // Role khách hàng: Active => Premium; Trial => Trial;
    // Expired/Cancelled => dựa vào từng có giao dịch thành công hay không.
    public static CustomerRole RoleOf(UserSubscription? cur, bool hadPaidTx)
    {
        if (cur == null) return CustomerRole.Trial;
        return cur.Status switch
        {
            SubscriptionStatus.Active => CustomerRole.Premium,
            SubscriptionStatus.Trial  => CustomerRole.Trial,
            _                         => hadPaidTx ? CustomerRole.Premium : CustomerRole.Trial
        };
    }

    // Nhãn tiếng Việt cho trạng thái gói.
    public static string StatusLabel(SubscriptionStatus s) => s switch
    {
        SubscriptionStatus.Trial     => "Dùng thử",
        SubscriptionStatus.Active    => "Đang hoạt động",
        SubscriptionStatus.Expired   => "Hết hạn",
        SubscriptionStatus.Cancelled => "Đã hủy",
        _ => s.ToString()
    };

    // Màu badge trạng thái (đồng bộ style admin).
    public static string StatusColor(SubscriptionStatus s) => s switch
    {
        SubscriptionStatus.Trial     => "#f59e0b",
        SubscriptionStatus.Active    => "#16a34a",
        SubscriptionStatus.Expired   => "#6b7280",
        SubscriptionStatus.Cancelled => "#dc2626",
        _ => "#6b7280"
    };

    public static string RoleLabel(CustomerRole r) => r == CustomerRole.Premium ? "Premium" : "Dùng thử";

    // ============ Dùng chung cho Admin & Nhân viên ============
    // Xây toàn bộ danh sách khách hàng (User có >=1 subscription), chưa lọc.
    public static async Task<List<CustomerListItem>> BuildAllAsync(AppDbContext db)
    {
        var allSubs = await db.UserSubscriptions.Include(s => s.Plan).ToListAsync();
        if (allSubs.Count == 0) return new();

        var subUserIds = allSubs.Select(s => s.UserId).Distinct().ToList();
        var users = await db.Users.Where(u => subUserIds.Contains(u.Id) && !u.IsDeleted).ToListAsync();
        var paid = (await db.Transactions.Where(t => t.Status == TransactionStatus.Success)
                        .Select(t => t.UserId).Distinct().ToListAsync()).ToHashSet();
        var byUser = allSubs.GroupBy(s => s.UserId).ToDictionary(g => g.Key, g => g.ToList());

        var res = new List<CustomerListItem>();
        foreach (var u in users)
        {
            if (!byUser.TryGetValue(u.Id, out var us)) continue;
            var cur = PickCurrent(us);
            if (cur == null) continue;
            res.Add(new CustomerListItem
            {
                UserId = u.Id, FullName = u.FullName, Email = u.Email,
                AvatarUrl = u.AvatarUrl, Phone = u.Phone,
                Role = RoleOf(cur, paid.Contains(u.Id)),
                PlanId = cur.PlanId, PlanName = cur.Plan?.Name ?? "(gói đã xóa)",
                SubStatus = cur.Status, AccountActive = u.IsActive,
                StartDate = cur.StartDate, EndDate = cur.EndDate, CurrentSubId = cur.Id
            });
        }
        return res;
    }

    // Áp bộ lọc role / trạng thái / gói / từ khóa.
    public static List<CustomerListItem> ApplyFilter(List<CustomerListItem> items,
        string? role, string? status, int? planId, string? q)
    {
        IEnumerable<CustomerListItem> x = items;
        if (role == "Premium") x = x.Where(i => i.Role == CustomerRole.Premium);
        else if (role == "Trial") x = x.Where(i => i.Role == CustomerRole.Trial);
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<SubscriptionStatus>(status, out var st))
            x = x.Where(i => i.SubStatus == st);
        if (planId.HasValue) x = x.Where(i => i.PlanId == planId.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.Trim().ToLower();
            x = x.Where(i => i.FullName.ToLower().Contains(ql)
                          || i.Email.ToLower().Contains(ql)
                          || (i.Phone ?? "").ToLower().Contains(ql));
        }
        return x.ToList();
    }

    // Ghi 1 dòng nhật ký thao tác (caller tự SaveChanges).
    public static void AddLog(AppDbContext db, int userId, string actorRole, string actorName,
        string action, string? oldV, string? newV, string? note)
    {
        db.CustomerActivityLogs.Add(new CustomerActivityLog
        {
            UserId = userId,
            ActorRole = actorRole,
            ActorName = string.IsNullOrWhiteSpace(actorName) ? "Hệ thống" : actorName,
            Action = action,
            OldValue = oldV,
            NewValue = newV,
            Note = note,
            CreatedAt = DateTime.Now
        });
    }

    // ============ Thao tác Premium (dùng chung Admin & Nhân viên) ============
    // Đăng ký Premium thủ công: tạo gói Active + giao dịch thành công.
    public static async Task<(bool ok, string msg, int userId)> RegisterPremiumAsync(
        AppDbContext db, int userId, int planId, int days, string actorRole, string actorName)
    {
        var u = await db.Users.FindAsync(userId);
        if (u == null || u.IsDeleted) return (false, "Không tìm thấy khách hàng.", 0);
        var plan = await db.SubscriptionPlans.FindAsync(planId);
        if (plan == null) return (false, "Gói không hợp lệ.", userId);

        int dur = days > 0 ? days : (plan.DurationDays > 0 ? plan.DurationDays : 30);
        var sub = new UserSubscription
        {
            UserId = userId, PlanId = planId, Status = SubscriptionStatus.Active,
            StartDate = DateTime.Now, EndDate = DateTime.Now.AddDays(dur),
            ConfirmedAt = DateTime.Now, CreatedAt = DateTime.Now,
            Notes = $"Đăng ký Premium thủ công bởi {actorName} ({DateTime.Now:dd/MM/yyyy HH:mm})"
        };
        db.UserSubscriptions.Add(sub);
        await db.SaveChangesAsync();

        db.Transactions.Add(new Transaction
        {
            UserId = userId, PlanId = planId, UserSubscriptionId = sub.Id, Amount = plan.Price,
            PaymentMethodLabel = "Đăng ký thủ công", Status = TransactionStatus.Success,
            CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now
        });
        AddLog(db, userId, actorRole, actorName, "Đăng ký Premium", "", $"{plan.Name} · {dur} ngày", null);
        await db.SaveChangesAsync();
        return (true, $"Đã đăng ký Premium ({plan.Name}) cho khách hàng.", userId);
    }

    // Gia hạn Premium: cộng thêm ngày, kích hoạt lại nếu đang hết hạn/hủy.
    public static async Task<(bool ok, string msg, int userId)> ExtendPremiumAsync(
        AppDbContext db, int subId, int days, string actorRole, string actorName)
    {
        var s = await db.UserSubscriptions.FindAsync(subId);
        if (s == null) return (false, "Không tìm thấy gói.", 0);
        if (days <= 0) return (false, "Số ngày phải lớn hơn 0.", s.UserId);

        var baseDate = s.EndDate > DateTime.Now ? s.EndDate : DateTime.Now;
        var old = s.EndDate;
        s.EndDate = baseDate.AddDays(days);
        if (s.Status == SubscriptionStatus.Expired || s.Status == SubscriptionStatus.Cancelled)
        { s.Status = SubscriptionStatus.Active; s.ConfirmedAt ??= DateTime.Now; }
        s.Notes = (string.IsNullOrEmpty(s.Notes) ? "" : s.Notes + " | ")
                + $"Gia hạn +{days} ngày bởi {actorName} ({DateTime.Now:dd/MM/yyyy HH:mm})";
        AddLog(db, s.UserId, actorRole, actorName, "Gia hạn Premium",
            old.ToString("dd/MM/yyyy"), s.EndDate.ToString("dd/MM/yyyy"), $"+{days} ngày");
        await db.SaveChangesAsync();
        return (true, $"Đã gia hạn thêm {days} ngày (hết hạn {s.EndDate:dd/MM/yyyy}).", s.UserId);
    }

    // Chuyển Trial -> Premium: kích hoạt gói hiện tại thành Active + ghi giao dịch.
    public static async Task<(bool ok, string msg, int userId)> ConvertTrialAsync(
        AppDbContext db, int subId, int planId, int days, string actorRole, string actorName)
    {
        var s = await db.UserSubscriptions.FindAsync(subId);
        if (s == null) return (false, "Không tìm thấy gói.", 0);
        var plan = await db.SubscriptionPlans.FindAsync(planId);
        if (plan == null) return (false, "Gói không hợp lệ.", s.UserId);

        int dur = days > 0 ? days : (plan.DurationDays > 0 ? plan.DurationDays : 30);
        var oldStatus = StatusLabel(s.Status);
        s.PlanId = planId;
        s.Status = SubscriptionStatus.Active;
        s.ConfirmedAt ??= DateTime.Now;
        s.StartDate = DateTime.Now;
        s.EndDate = DateTime.Now.AddDays(dur);
        s.Notes = (string.IsNullOrEmpty(s.Notes) ? "" : s.Notes + " | ")
                + $"Chuyển Trial→Premium bởi {actorName} ({DateTime.Now:dd/MM/yyyy HH:mm})";
        db.Transactions.Add(new Transaction
        {
            UserId = s.UserId, PlanId = planId, UserSubscriptionId = s.Id, Amount = plan.Price,
            PaymentMethodLabel = "Nâng cấp từ Trial", Status = TransactionStatus.Success,
            CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now
        });
        AddLog(db, s.UserId, actorRole, actorName, "Chuyển Trial → Premium", oldStatus, plan.Name, null);
        await db.SaveChangesAsync();
        return (true, $"Đã chuyển sang Premium ({plan.Name}).", s.UserId);
    }

    // Dựng dữ liệu chi tiết 1 khách hàng. Trả null nếu không phải khách hàng Premium/Trial.
    public static async Task<CustomerDetailVM?> BuildDetailAsync(AppDbContext db, int userId)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
        if (user == null) return null;

        var subs = await db.UserSubscriptions.Include(s => s.Plan)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt).ToListAsync();
        if (subs.Count == 0) return null;   // ngoài phạm vi module

        var txs = await db.Transactions.Include(t => t.Plan)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt).ToListAsync();
        var logs = await db.CustomerActivityLogs
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt).ToListAsync();

        var cur = PickCurrent(subs);
        bool hadPaid = txs.Any(t => t.Status == TransactionStatus.Success);
        var planNames = subs.Where(s => s.Plan != null)
            .Select(s => s.Plan!).GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);

        return new CustomerDetailVM
        {
            User = user,
            Current = cur,
            CurrentPlanName = cur?.Plan?.Name,
            Role = RoleOf(cur, hadPaid),
            History = subs,
            PlanNames = planNames,
            Transactions = txs,
            Logs = logs,
            HadPaidTx = hadPaid
        };
    }
}
