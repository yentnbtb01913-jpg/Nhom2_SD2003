using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using WisdomITNews.Data;
using WisdomITNews.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = VideoUploadService.MaxSize);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = VideoUploadService.MaxSize);

builder.Services.AddScoped<WisdomITNews.Filters.AdminAuditFilter>();
builder.Services.AddControllersWithViews(o => o.Filters.Add<WisdomITNews.Filters.AdminAuditFilter>()).AddNewtonsoftJson();
builder.Services.AddHttpClient();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<AIService>();
builder.Services.Configure<WisdomITNews.Models.AiOptions>(builder.Configuration.GetSection("AI"));
builder.Services.AddScoped<EmailService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<WeatherService>();
builder.Services.AddScoped<ImageUploadService>();
builder.Services.AddScoped<VideoUploadService>();
builder.Services.AddScoped<NewsImportService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<PodcastService>();
builder.Services.AddScoped<EmailConfirmationService>();
builder.Services.AddScoped<ExternalArticleService>();
builder.Services.AddScoped<FeaturedArticleService>();
builder.Services.AddHostedService<WisdomITNews.Services.SubscriptionBackgroundService>();
builder.Services.AddHostedService<WisdomITNews.Services.AutoImportBackgroundService>();
builder.Services.AddSignalR();
builder.Services.AddSession(opt =>
{
    opt.IdleTimeout = TimeSpan.FromHours(8);
    opt.Cookie.HttpOnly = true;
    opt.Cookie.IsEssential = true;
});

// ===== Đăng nhập ngoài: Google / Facebook (khoá đọc từ config / User Secrets) =====
var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
}).AddCookie();

var googleId = builder.Configuration["Authentication:Google:ClientId"];
var googleSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleId) && !string.IsNullOrWhiteSpace(googleSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleId;
        options.ClientSecret = googleSecret;
        options.SignInScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
    });
}

var fbId = builder.Configuration["Authentication:Facebook:AppId"];
var fbSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
if (!string.IsNullOrWhiteSpace(fbId) && !string.IsNullOrWhiteSpace(fbSecret))
{
    authBuilder.AddFacebook(options =>
    {
        options.AppId = fbId;
        options.AppSecret = fbSecret;
        options.SignInScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
        options.Scope.Clear();
        options.Scope.Add("public_profile"); // chỉ xin public_profile -> không cần quyền email
    });
}

var app = builder.Build();

// Auto migrate
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Tự bổ sung các Slot QC mới (idempotent: đã có mã thì bỏ qua) để 4 vùng quảng cáo mới bán được.
    var wantSlots = new[]
    {
        // Các vị trí gốc + dải dọc trái/phải (đảm bảo đủ nếu chưa có / lỡ bị xóa)
        new WisdomITNews.Models.AdSlot { Name = "Banner Đầu Trang (ngang)", SlotKey = "header",     Description = "Banner ngang 728×90 nằm trên cùng, phía trên cả logo — mọi trang", Size = "Banner728x90", PricePerDay = 500_000m },
        new WisdomITNews.Models.AdSlot { Name = "Giữa Bài Viết",            SlotKey = "in_article", Description = "Banner 728×90 chèn giữa nội dung bài viết", Size = "Banner728x90", PricePerDay = 400_000m },
        new WisdomITNews.Models.AdSlot { Name = "Cạnh Bài Viết",            SlotKey = "sidebar",    Description = "Quảng cáo 300×250 ở sidebar cạnh bài viết", Size = "Rectangle300x250", PricePerDay = 250_000m },

        new WisdomITNews.Models.AdSlot { Name = "Billboard Giữa Trang Chủ", SlotKey = "home_billboard", Description = "Banner lớn 970×250 xen giữa trang chủ", Size = "Billboard970x250", PricePerDay = 600_000m },
        new WisdomITNews.Models.AdSlot { Name = "Banner Cuối Trang Chủ",   SlotKey = "home_bottom",    Description = "Banner ngang 970×90 trước mục Đừng bỏ lỡ", Size = "Banner970x90", PricePerDay = 300_000m },
        new WisdomITNews.Models.AdSlot { Name = "Billboard Dưới Bài Viết", SlotKey = "article_billboard", Description = "Banner lớn 970×250 dưới nội dung bài", Size = "Billboard970x250", PricePerDay = 500_000m },
        new WisdomITNews.Models.AdSlot { Name = "Dải Dọc Cạnh Bài Viết",   SlotKey = "article_side600", Description = "Skyscraper 300×600 sticky cạnh bài viết", Size = "Skyscraper300x600", PricePerDay = 350_000m },
        // Bổ sung danh phận cho các ô placeholder còn lại
        new WisdomITNews.Models.AdSlot { Name = "Banner Dưới Menu",        SlotKey = "topbanner",       Description = "Banner ngang 970×120 ngay dưới thanh menu — mọi trang", Size = "Banner970x120", PricePerDay = 450_000m },
        new WisdomITNews.Models.AdSlot { Name = "Hộp Khu Nổi Bật",         SlotKey = "home_focus_rect", Description = "Hộp 300×250 ở sidebar khu tin nổi bật trang chủ", Size = "Rectangle300x250", PricePerDay = 250_000m },
        new WisdomITNews.Models.AdSlot { Name = "Dải 3 Hộp Trang Chủ #1",  SlotKey = "home_rect_1",     Description = "Hộp 300×250 (ô 1) trong dải 3 quảng cáo trang chủ", Size = "Rectangle300x250", PricePerDay = 200_000m },
        new WisdomITNews.Models.AdSlot { Name = "Dải 3 Hộp Trang Chủ #2",  SlotKey = "home_rect_2",     Description = "Hộp 300×250 (ô 2) trong dải 3 quảng cáo trang chủ", Size = "Rectangle300x250", PricePerDay = 200_000m },
        new WisdomITNews.Models.AdSlot { Name = "Dải 3 Hộp Trang Chủ #3",  SlotKey = "home_rect_3",     Description = "Hộp 300×250 (ô 3) trong dải 3 quảng cáo trang chủ", Size = "Rectangle300x250", PricePerDay = 200_000m },
        new WisdomITNews.Models.AdSlot { Name = "Banner Giữa Cột Trang Chủ", SlotKey = "home_mid_banner", Description = "Banner 970×90 xen giữa cột nội dung trang chủ", Size = "Banner970x90", PricePerDay = 280_000m },
        new WisdomITNews.Models.AdSlot { Name = "Dải Dọc Chuyên Mục",      SlotKey = "home_side600",    Description = "Skyscraper 300×600 sticky ở sidebar khu chuyên mục", Size = "Skyscraper300x600", PricePerDay = 320_000m },
        new WisdomITNews.Models.AdSlot { Name = "Hộp 300×250 Trang Bài",   SlotKey = "article_rect",    Description = "Hộp 300×250 ở sidebar trang chi tiết bài", Size = "Rectangle300x250", PricePerDay = 260_000m },
    };
    var existingKeys = db.AdSlots.Select(s => s.SlotKey).ToList();
    var toAdd = wantSlots.Where(s => !existingKeys.Contains(s.SlotKey)).ToList();
    if (toAdd.Count > 0)
    {
        db.AdSlots.AddRange(toAdd);
        db.SaveChanges();
    }

}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapHub<WisdomITNews.Hubs.ChatHub>("/chatHub");
app.MapHub<WisdomITNews.Hubs.NotificationHub>("/hubs/notification");
app.MapControllerRoute(name: "article",   pattern: "bai-viet/{slug}",    defaults: new { controller = "Article",  action = "Detail" });
app.MapControllerRoute(name: "category",  pattern: "danh-muc/{slug?}",   defaults: new { controller = "Home",     action = "Category" });
app.MapControllerRoute(name: "search",    pattern: "tim-kiem",            defaults: new { controller = "Home",     action = "Search" });
app.MapControllerRoute(name: "journalist", pattern: "journalist/{action=Dashboard}/{id?}", defaults: new { controller = "Journalist" });
app.MapControllerRoute(name: "admin",     pattern: "admin/{action=Dashboard}/{id?}", defaults: new { controller = "Admin" });
app.MapControllerRoute(name: "staff",     pattern: "nhan-vien/{action=Login}/{id?}", defaults: new { controller = "NhanVien" });
app.MapControllerRoute(name: "account",   pattern: "Account/{action=Login}", defaults: new { controller = "Account" });
app.MapControllerRoute(name: "default",   pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
