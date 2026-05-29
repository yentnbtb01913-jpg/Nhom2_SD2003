using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews().AddNewtonsoftJson();
builder.Services.AddHttpClient();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<AIService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<WeatherService>();
builder.Services.AddScoped<ImageUploadService>();
builder.Services.AddSignalR();
builder.Services.AddSession(opt =>
{
    opt.IdleTimeout = TimeSpan.FromHours(8);
    opt.Cookie.HttpOnly = true;
    opt.Cookie.IsEssential = true;
});

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
app.UseAuthorization();

app.MapHub<WisdomITNews.Hubs.ChatHub>("/chatHub");
app.MapControllerRoute(name: "article",   pattern: "bai-viet/{slug}",    defaults: new { controller = "Article",  action = "Detail" });
app.MapControllerRoute(name: "category",  pattern: "danh-muc/{slug?}",   defaults: new { controller = "Home",     action = "Category" });
app.MapControllerRoute(name: "search",    pattern: "tim-kiem",            defaults: new { controller = "Home",     action = "Search" });
app.MapControllerRoute(name: "journalist", pattern: "journalist/{action=Dashboard}/{id?}", defaults: new { controller = "Journalist" });
app.MapControllerRoute(name: "admin",     pattern: "admin/{action=Dashboard}/{id?}", defaults: new { controller = "Admin" });
app.MapControllerRoute(name: "account",   pattern: "Account/{action=Login}", defaults: new { controller = "Account" });
app.MapControllerRoute(name: "default",   pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
