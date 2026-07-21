# WisdomITNews — Tổng quan dự án

> Tài liệu tham chiếu nội bộ (Nhóm 2 – SD2003). Mục đích: nắm nhanh kiến trúc để sửa code/thêm tính năng nhất quán với codebase. Cập nhật: 2026-07-14.

## 1. Stack & thông tin chung

- **Framework:** ASP.NET Core MVC 8.0 (single project, không tách layer/solution phụ). `Nullable` + `ImplicitUsings` bật.
- **ORM:** Entity Framework Core 8 (Code-First, SQL Server, `Server=YAAI\SQLEXPRESS;Database=WisdomITNews`).
- **Real-time:** SignalR (4 hub).
- **AI:** Google Gemini (`gemini-2.5-flash`) qua `HttpClient` thô; có cấu hình Groq dự phòng.
- **Auth ngoài:** Google + Facebook (chỉ bật khi có key), nhưng auth chính là **session-based tự viết**, không dùng ASP.NET Identity.
- **Package chính:** `Microsoft.EntityFrameworkCore.SqlServer`, `EntityFrameworkCore.Tools`, `Mvc.NewtonsoftJson`, `BCrypt.Net-Next` (hash mật khẩu), `Newtonsoft.Json`, `SixLabors.ImageSharp` (xử lý ảnh), `Authentication.Google`/`.Facebook`.
- **Front-end (CDN, không bundler):** Font Awesome 6.5, SignalR JS 8.0.7, GLightbox (lightbox ảnh), SweetAlert2, Google Fonts (Playfair Display + Inter). CSS viết tay: `site.css`, `baomoi-skin.css`. JS: `site.js`. Ngoài ra (theo skill dự án): TinyMCE (editor), Leaflet (bản đồ vùng miền).

## 2. Sơ đồ thư mục chính

```
WisdomITNews/
├── Program.cs            # Entry point: DI, middleware, route, auto-migrate, map hub
├── appsettings.json      # ConnectionString, Gemini/Groq, SMTP, OpenWeather, AI prompt templates
├── Controllers/          # 14 controller (xem §4)
├── Models/               # Entity + ViewModel, gom theo nhóm file (xem §3)
├── Data/AppDbContext.cs  # DbContext: 40+ DbSet, toàn bộ cấu hình quan hệ/index/seed
├── Migrations/           # ~30 migration Code-First (nhiều file .bak nên đếm thô là 79)
├── Services/             # Business logic + tích hợp ngoài (xem §5)
├── Hubs/                 # ChatHub, AdChatHub, TeamChatHub, NotificationHub
├── ViewComponents/       # CategoryMenu, AdSlot, AdRenewalBadge, VideoSidebar, Weather
├── Views/                # Razor, tách theo controller (Admin 67 view, NhanVien 26, Account 12...)
├── Scripts/              # SQL thủ công (apply-video-upload-migration.sql)
└── wwwroot/
    ├── css/ (site.css, baomoi-skin.css)  js/ (site.js)
    └── uploads/ (articles, audio, avatars, chat, content, covers, videos)
```

> **Lưu ý dọn dẹp:** rất nhiều file `*.cs.bak*` / `appsettings.json.bak*` nằm rải rác (backup thủ công). **Không** sửa/tham chiếu các file `.bak` — chỉ dùng file gốc. Cân nhắc gitignore/xoá sau.

## 3. Model/Entity chính & quan hệ

Entity gom trong `Models/` theo domain (không mỗi entity một file). File `ViewModels.cs` chứa toàn bộ ViewModel/DTO (`HomeViewModel`, `ArticleFormViewModel`, `ApiResponse<T>`, `ModerationResult`, `RegisterViewModel`...).

**Nhóm nội dung (cốt lõi):**
- `Article` — trung tâm. FK tùy chọn: `CategoryId`→`Category`, `AuthorId`→`Admin`, `AuthorUserId`→`User` (bài do 3 loại tác giả tạo, tất cả `SetNull` khi xoá). Cờ: `Status` (draft/published), `IsFeatured/IsBreaking/IsPremiumOnly`, `FeaturedPinned/FeaturedHidden` (tin nổi bật tự động), `IsExternal`+`SourceName/SourceUrl` (bài RSS), `Region/Latitude/Longitude` (bản đồ). `Slug` unique.
- `Category` — tự tham chiếu (`ParentCategoryId`, cây danh mục, `Restrict`). Seed sẵn 8 danh mục.
- `Tag` ↔ `Article` qua `ArticleTag` (khoá phức `{ArticleId, TagId}`).
- `Comment` — gắn `Article` (Cascade), tự tham chiếu `ParentCommentId` (reply, Restrict), `User` (SetNull). `CommentVote` unique theo `{CommentId, SessionId}`.
- `ViewHistory`, `SearchHistory`, `SeedViewBatch` — thống kê lượt xem/tìm kiếm (`SeedViewBatch` = lượt xem ảo cho "Kênh Bên Ngoài", lưu delta JSON để hoàn tác).

**Nhóm người dùng & vai trò (3 hệ auth tách biệt):**
- `Admin` — ban quản lý/nhân viên toà soạn. `Role` (superadmin/editor...), `EmploymentStatus` (working/on_leave/resigned/terminated). Seed sẵn `admin`/`password`. `StaffProfile` (1-1 `AdminId`) + `StaffActivityLog`.
- `User` — độc giả/khách hàng. `Role` (Reader...), `EmailVerified`, `HasUsedTrial`, `IsDeleted`. `CustomerActivityLog` = audit thao tác khách hàng.
- `JournalistProfile` — nhà báo (1-1 với `User.Id`).

**Nhóm social & chat:**
- `Friendship` (Requester/Receiver, unique cặp, có `Status`), `UserFollow` (Follower/Following).
- `ChatGroup` → `ChatMember` / `ChatMessage` (Cascade). `ChatMember` đa hình qua `{MemberType, MemberId}`. `ChatMessage.ReplyToId` tự tham chiếu.
- `TeamChatMessage` (chat nội bộ ban quản lý, gom theo `ConversationKey`), `AdRenewalMessage` (chat gia hạn quảng cáo, gắn `Advertisement`).
- `SavedArticle`, `UserCategoryFollow` (khu cá nhân, unique theo cặp user–target).
- `Notification` (thông báo real-time).

**Nhóm nghiệp vụ khác:**
- Premium: `SubscriptionPlan` → `PlanFeature`, `UserSubscription`, `Transaction` (decimal(18,2), enum `SubscriptionStatus`/`TransactionStatus`).
- Quảng cáo: `Advertisement` + `AdRenewalMessage`.
- Media: `Video` + `VideoComment` (Cascade theo `Video`, KHÔNG cấu hình self-FK cho reply — giữ phẳng), `Podcast` (gắn `Article`, SetNull).
- RSS/Import: `RssSource`, `AutoImportSettings`, `CategoryMapping` + `AiCategoryCorrectionLog` (ánh xạ nhãn AI → danh mục).
- AI/email: `AILog`, `AiSetting`, `NewsletterSubscriber` + `NewsletterEmailLog`, `EmailConfirmationToken` (enum `EmailTokenPurpose`), `FeedbackReport`.

> Toàn bộ quan hệ, index, `HasMaxLength`, `DeleteBehavior`, và seed data đều khai báo tập trung trong `AppDbContext.OnModelCreating` — **sửa quan hệ/thêm entity thì cập nhật ở đây rồi tạo migration**.

## 4. Luồng xử lý & routing

Pattern: **Controller (dày) → Service → EF Core (`AppDbContext`) trực tiếp**. **Không có repository/UnitOfWork**; controller inject thẳng `AppDbContext` + service. Controller khá to (Admin 3367 dòng, NhanVien 1425, Account 1155, Journalist 1086) — logic nghiệp vụ nằm nhiều ở controller.

**Route tùy biến (SEO tiếng Việt, khai trong `Program.cs`):**
- `bai-viet/{slug}` → `Article/Detail`
- `danh-muc/{slug?}` → `Home/Category`
- `tim-kiem` → `Home/Search`
- `journalist/{action=Dashboard}` → `Journalist`
- `admin/{action=Dashboard}` → `Admin`
- `nhan-vien/{action=Login}` → `NhanVien`
- `Account/{action=Login}` → `Account`
- default `{controller=Home}/{action=Index}/{id?}`

**14 controller:** Home, Article, Account (độc giả), Journalist (nhà báo), Admin (quản trị), NhanVien (nhân viên toà soạn), Chat, Friend, Subscription (premium), Rss, Region, Video, Feedback, Ad.

**Hub SignalR:** `/chatHub` (ChatHub – chat nhóm/DM, pin, upload file, typing, online, friend/follow), `/adChatHub`, `/teamChatHub`, `/hubs/notification`.

**Middleware order (`Program.cs`):** HttpsRedirect → StaticFiles → Routing → **Session** → Authentication → Authorization. Auto-`db.Database.Migrate()` khi khởi động.

## 5. Services (DI: hầu hết `Scoped`)

- `AIService` — Gemini qua HttpClient thô. API: `SummarizeAsync`, `SuggestTitlesAsync`, `ModerateContentAsync` (chấm điểm 0–100), `ChatAsync` (chatbot), `ClassifyCategoryAsync` (phân loại danh mục AI). Prompt template cấu hình trong `appsettings.json → AI`.
- `EmailService` + `EmailConfirmationService` — SMTP Gmail (cấu hình `Smtp`).
- `WeatherService` — OpenWeather. `ImageUploadService`/`VideoUploadService` — upload media (`VideoUploadService.MaxSize` set giới hạn Kestrel/Form). `PodcastService` — TTS (Piper, cấu hình `Piper`).
- `NewsImportService` + `ExternalArticleService` — nhập bài RSS/nguồn ngoài. `FeaturedArticleService` — tin nổi bật tự động. `NotificationService` — bắn thông báo.
- **HostedService (chạy nền):** `SubscriptionBackgroundService` (hết hạn premium), `AutoImportBackgroundService` (tự nhập RSS ~1 bài/phút).
- Helper: `SlugHelper`, `CustomerHelper`, `PremiumAccess`, `SimpleXlsx` (xuất Excel).

## 6. Điểm cần nhớ khi sửa code (convention & bẫy)

- **Auth = session tự viết, 3 hệ tách biệt** (không Identity, không `[Authorize]` theo role chuẩn). Kiểm tra quyền bằng đọc session key:
  - Độc giả/khách hàng: `Session["UserId"]` (int), `UserRole`, `UserEmail`, `UserName`, `UserAvatar`, `EmailVerified`.
  - Admin/nhân viên: `Session["AdminId"]`, `AdminName`, `AdminRole`.
  - Nhà báo: `Session["JournalistEmail"]`, `JournalistName`, `JournalistAvatar`.
  - → Khi thêm action cần bảo vệ, **tự check session** theo đúng nhóm; không có filter tập trung.
- **Mật khẩu:** hash bằng `BCrypt.Net` — luôn dùng `BCrypt.Verify`/`HashPassword`, không so sánh chuỗi thô.
- **EF:** dùng `Include`/projection để tránh N+1 (nhiều navigation collection trên `Article`). FK phần lớn nullable + `SetNull` → luôn xử lý null khi đọc `Author/Category`.
- **Migration:** Code-First, tạo migration sau mỗi thay đổi model/`OnModelCreating`. App auto-migrate lúc chạy nên migration phải sạch. SQL thủ công để trong `Scripts/`.
- **Slug:** `Article/Category/Tag` slug unique — sinh qua `SlugHelper`, kiểm tra trùng trước khi lưu.
- **Ngôn ngữ:** UI, comment code, tên nghiệp vụ đều tiếng Việt. Route tiếng Việt không dấu. Giữ nhất quán khi đặt tên/route mới.
- **Views:** tổ chức theo controller; component tái dùng qua `ViewComponents` + `Views/Shared`. Không có SPA/bundler — thêm lib bằng thẻ CDN trong `_Layout.cshtml`, CSS viết tay.
- **⚠️ Bảo mật (nên xử lý):** `appsettings.json` đang commit **khoá thật** (Gemini/Groq API key, mật khẩu SMTP Gmail, OpenWeather). Nên chuyển sang User Secrets/biến môi trường và **xoay lại các khoá đã lộ**; đã có sẵn `appsettings.Example.json` làm mẫu.
- **File rác:** dọn các `*.bak*` trước khi nộp/bàn giao để tránh nhầm lẫn khi đọc code.

---
*Chỉ đọc & phân tích — chưa chỉnh sửa bất kỳ file mã nguồn nào của dự án ở bước này.*
