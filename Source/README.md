# Wisdom IT News — ASP.NET Core MVC

Hệ thống báo điện tử IT tích hợp AI, port từ PHP sang C# ASP.NET Core MVC.

## Công nghệ
- ASP.NET Core MVC 8.0
- Entity Framework Core + SQL Server
- Gemini AI API (tóm tắt, gợi ý tiêu đề)
- BCrypt (mã hóa mật khẩu)

## Tính năng
- 📰 Trang chủ, danh mục, tìm kiếm, bài viết chi tiết
- 🤖 AI tóm tắt bài báo & gợi ý tiêu đề (Gemini)
- 💬 Hệ thống bình luận + kiểm duyệt
- 📧 Newsletter đăng ký
- 🛠️ Admin panel: quản lý bài viết, danh mục, bình luận, AI logs
- 🔐 Đăng nhập admin với BCrypt

## Cách chạy

### 1. Yêu cầu
- .NET 8 SDK
- SQL Server (LocalDB hoặc SQL Server Express)
- Gemini API key (free tại aistudio.google.com)

### 2. Cấu hình
Mở `appsettings.json`, thay:
```json
"DefaultConnection": "Server=localhost;Database=WisdomITNews;Trusted_Connection=True;TrustServerCertificate=True;"
"ApiKey": "YOUR_GEMINI_API_KEY_HERE"
```

### 3. Chạy
```bash
dotnet run
```
Database sẽ tự tạo khi chạy lần đầu (auto migrate).

### 4. Đăng nhập Admin
- URL: `/admin/Login`
- Username: `admin`
- Password: `password`

## Cấu trúc project
```
WisdomITNews/
├── Controllers/
│   ├── HomeController.cs       ← Trang chủ, danh mục, tìm kiếm
│   ├── ArticleController.cs    ← Trang bài viết
│   ├── ApiController.cs        ← API: AI, comment, newsletter
│   └── AdminController.cs      ← Admin panel
├── Models/
│   ├── Models.cs               ← Entity models
│   └── ViewModels.cs           ← ViewModels
├── Data/
│   └── AppDbContext.cs         ← EF Core DbContext + seed data
├── Services/
│   ├── AIService.cs            ← Gọi Gemini API
│   └── SlugHelper.cs           ← Tiện ích slug, format
├── Views/
│   ├── Home/Index.cshtml       ← Trang chủ
│   ├── Article/Detail.cshtml   ← Bài viết chi tiết
│   ├── Admin/                  ← Admin views
│   └── Shared/_Layout.cshtml
└── wwwroot/
    ├── css/site.css            ← CSS từ PHP project
    └── js/site.js
```
