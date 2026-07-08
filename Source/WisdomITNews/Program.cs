using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using WisdomITNews.Data;
using WisdomITNews.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = VideoUploadService.MaxSize);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = VideoUploadService.MaxSize);

builder.Services.AddControllersWithViews().AddNewtonsoftJson();
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
app.MapHub<WisdomITNews.Hubs.AdChatHub>("/adChatHub");
app.MapHub<WisdomITNews.Hubs.TeamChatHub>("/teamChatHub");
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
