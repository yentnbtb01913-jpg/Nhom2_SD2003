using Microsoft.EntityFrameworkCore;
using WisdomITNews.Models;

namespace WisdomITNews.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Admin> Admins { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Article> Articles { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<ArticleTag> ArticleTags { get; set; }
    public DbSet<Comment> Comments { get; set; }
    public DbSet<AILog> AILogs { get; set; }

    // [A] new tables
    public DbSet<CommentVote> CommentVotes { get; set; }
    public DbSet<ViewHistory> ViewHistories { get; set; }
    public DbSet<FeedbackReport> FeedbackReports { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<ChatGroup> ChatGroups { get; set; }
    public DbSet<ChatMember> ChatMembers { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<Friendship> Friendships { get; set; }
    public DbSet<UserFollow> UserFollows { get; set; }
    public DbSet<SavedArticle> SavedArticles { get; set; }
    public DbSet<UserCategoryFollow> UserCategoryFollows { get; set; }
    public DbSet<Video> Videos { get; set; }
    public DbSet<JournalistProfile> JournalistProfiles { get; set; }
    public DbSet<RssSource> RssSources { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<VideoComment> VideoComments { get; set; }
    public DbSet<AiSetting> AiSettings { get; set; }
    public DbSet<StaffProfile> StaffProfiles { get; set; }
    public DbSet<StaffActivityLog> StaffActivityLogs { get; set; }
    public DbSet<Advertisement> Advertisements { get; set; }
    public DbSet<AdSlot> AdSlots { get; set; }
    public DbSet<AdZoneSetting> AdZoneSettings { get; set; }
    public DbSet<Podcast> Podcasts { get; set; }
    public DbSet<EmailConfirmationToken> EmailConfirmationTokens { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<AutoImportSettings> AutoImportSettings { get; set; }
    public DbSet<CategoryMapping> CategoryMappings { get; set; }
    public DbSet<AiCategoryCorrectionLog> AiCategoryCorrectionLogs { get; set; }
    public DbSet<SearchHistory> SearchHistories { get; set; }
    public DbSet<SeedViewBatch> SeedViewBatches { get; set; }
    public DbSet<AdBooking> AdBookings { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Số tiền đơn quảng cáo — khai báo precision rõ ràng để hết cảnh báo decimal
        mb.Entity<AdBooking>().Property(b => b.Amount).HasPrecision(18, 2);

        // ArticleTag composite key
        mb.Entity<ArticleTag>().HasKey(at => new { at.ArticleId, at.TagId });

        mb.Entity<ArticleTag>()
            .HasOne(at => at.Article).WithMany(a => a.ArticleTags).HasForeignKey(at => at.ArticleId);
        mb.Entity<ArticleTag>()
            .HasOne(at => at.Tag).WithMany(t => t.ArticleTags).HasForeignKey(at => at.TagId);

        mb.Entity<Article>()
            .HasOne(a => a.Category).WithMany(c => c.Articles).HasForeignKey(a => a.CategoryId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Article>()
            .HasOne(a => a.Author).WithMany(ad => ad.Articles).HasForeignKey(a => a.AuthorId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Article>()
            .HasOne(a => a.AuthorUser).WithMany(u => u.Articles).HasForeignKey(a => a.AuthorUserId).OnDelete(DeleteBehavior.SetNull);

        mb.Entity<Comment>()
            .HasOne(c => c.Article).WithMany(a => a.Comments).HasForeignKey(c => c.ArticleId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<Comment>()
            .HasOne(c => c.ParentComment).WithMany(c => c.Replies).HasForeignKey(c => c.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Comment>()
            .HasOne(c => c.User).WithMany(u => u.Comments).HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.SetNull);

        mb.Entity<CommentVote>()
            .HasOne(v => v.Comment).WithMany(c => c.Votes).HasForeignKey(v => v.CommentId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<CommentVote>().HasIndex(v => new { v.CommentId, v.SessionId }).IsUnique();

        mb.Entity<ViewHistory>()
            .HasOne(v => v.Article).WithMany(a => a.ViewHistories).HasForeignKey(v => v.ArticleId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ViewHistory>().HasIndex(v => v.SessionId);
        mb.Entity<ViewHistory>().HasIndex(v => v.ViewedAt);

        // Lịch sử tìm kiếm (tính năng tìm kiếm thông minh trên Trang chủ)
        mb.Entity<SearchHistory>().HasIndex(s => s.SessionId);
        mb.Entity<SearchHistory>().HasIndex(s => s.UserId);
        mb.Entity<SearchHistory>().HasIndex(s => s.SearchedAt);

        // Đợt lượt xem mẫu (Kênh Bên Ngoài)
        mb.Entity<SeedViewBatch>().HasIndex(b => b.CreatedAt);
        mb.Entity<SeedViewBatch>().HasIndex(b => b.Scope);

        // Tin nổi bật tự động (Trang chủ)
        mb.Entity<Article>().HasIndex(a => a.FeaturedPinned);
        mb.Entity<Article>().HasIndex(a => a.FeaturedHidden);

        mb.Entity<Category>()
            .HasOne(c => c.ParentCategory).WithMany(c => c.Children).HasForeignKey(c => c.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);

        // Indexes
        mb.Entity<Article>().HasIndex(a => a.Slug).IsUnique();
        mb.Entity<Article>().HasIndex(a => a.Status);
        mb.Entity<Article>().HasIndex(a => a.Region);
        mb.Entity<Category>().HasIndex(c => c.Slug).IsUnique();
        mb.Entity<Tag>().HasIndex(t => t.Slug).IsUnique();
        mb.Entity<Admin>().HasIndex(a => a.Username).IsUnique();
        mb.Entity<Admin>().Property(a => a.EmploymentStatus).HasDefaultValue("working");
        mb.Entity<User>().HasIndex(u => u.Username).IsUnique();
        mb.Entity<User>().HasIndex(u => u.Email).IsUnique();
        mb.Entity<FeedbackReport>().HasIndex(f => f.IsResolved);
        mb.Entity<FeedbackReport>().HasIndex(f => f.CreatedAt);

        // Seed admin
        mb.Entity<Admin>().HasData(new Admin
        {
            Id = 1,
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
            FullName = "Quản Trị Viên",
            Email = "admin@wisdomitnews.vn",
            Role = "superadmin",
            IsActive = true,
            CreatedAt = new DateTime(2025, 1, 1)
        });

        // Seed categories
        mb.Entity<Category>().HasData(
            new Category { Id=1, Name="Tin Công Nghệ",         Slug="tin-cong-nghe",       Icon="📰", Color="#e63946", SortOrder=1, CreatedAt=new DateTime(2025,1,1), UpdatedAt=new DateTime(2025,1,1) },
            new Category { Id=2, Name="Lập Trình",             Slug="lap-trinh",           Icon="💻", Color="#2196f3", SortOrder=2, CreatedAt=new DateTime(2025,1,1), UpdatedAt=new DateTime(2025,1,1) },
            new Category { Id=3, Name="AI & Machine Learning", Slug="ai-machine-learning", Icon="🤖", Color="#6c63ff", SortOrder=3, CreatedAt=new DateTime(2025,1,1), UpdatedAt=new DateTime(2025,1,1) },
            new Category { Id=4, Name="Phần Mềm",              Slug="phan-mem",            Icon="📱", Color="#00897b", SortOrder=4, CreatedAt=new DateTime(2025,1,1), UpdatedAt=new DateTime(2025,1,1) },
            new Category { Id=5, Name="Phần Cứng",             Slug="phan-cung",           Icon="🖥️", Color="#f77f00", SortOrder=5, CreatedAt=new DateTime(2025,1,1), UpdatedAt=new DateTime(2025,1,1) },
            new Category { Id=6, Name="Thủ Thuật IT",          Slug="thu-thuat-it",        Icon="💡", Color="#ffd166", SortOrder=6, CreatedAt=new DateTime(2025,1,1), UpdatedAt=new DateTime(2025,1,1) },
            new Category { Id=7, Name="Bảo Mật",               Slug="bao-mat",             Icon="🔐", Color="#c1121f", SortOrder=7, CreatedAt=new DateTime(2025,1,1), UpdatedAt=new DateTime(2025,1,1) },
            new Category { Id=8, Name="Điện Toán Đám Mây",     Slug="dien-toan-dam-may",   Icon="☁️", Color="#0077b6", SortOrder=8, CreatedAt=new DateTime(2025,1,1), UpdatedAt=new DateTime(2025,1,1) }
        );

        // Friendship relationships
        mb.Entity<Friendship>()
            .HasOne(f => f.Requester).WithMany().HasForeignKey(f => f.RequesterId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Friendship>()
            .HasOne(f => f.Receiver).WithMany().HasForeignKey(f => f.ReceiverId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Friendship>().HasIndex(f => new { f.RequesterId, f.ReceiverId }).IsUnique();
        mb.Entity<Friendship>().HasIndex(f => f.Status);

        // Follow relationships
        mb.Entity<UserFollow>()
            .HasOne(f => f.Follower).WithMany().HasForeignKey(f => f.FollowerId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<UserFollow>()
            .HasOne(f => f.Following).WithMany().HasForeignKey(f => f.FollowingId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<UserFollow>().HasIndex(f => new { f.FollowerId, f.FollowingId }).IsUnique();

        // Chat relationships
        mb.Entity<ChatMember>()
            .HasOne(m => m.Group).WithMany(g => g.Members).HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ChatMessage>()
            .HasOne(m => m.Group).WithMany(g => g.Messages).HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<ChatMessage>()
            .HasOne(m => m.ReplyTo).WithMany().HasForeignKey(m => m.ReplyToId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<ChatMessage>().HasIndex(m => m.GroupId);
        mb.Entity<ChatMessage>().HasIndex(m => m.SentAt);
        mb.Entity<ChatMember>().HasIndex(m => new { m.GroupId, m.MemberType, m.MemberId }).IsUnique();

        // Khu cá nhân
        mb.Entity<SavedArticle>().HasIndex(x => new { x.UserId, x.ArticleId }).IsUnique();
        mb.Entity<UserCategoryFollow>().HasIndex(x => new { x.UserId, x.CategoryId }).IsUnique();
        mb.Entity<JournalistProfile>().HasIndex(x => x.UserId).IsUnique();
        mb.Entity<StaffProfile>().HasIndex(x => x.AdminId).IsUnique();
        mb.Entity<StaffActivityLog>().HasIndex(x => x.CreatedAt);
        mb.Entity<StaffActivityLog>().HasIndex(x => x.TargetAdminId);

        // Bình luận video — FK tới Video (xóa video -> xóa bình luận). KHÔNG cấu hình self-FK cho ParentId (giữ phẳng, tránh đệ quy).
        mb.Entity<VideoComment>()
            .HasOne(c => c.Video).WithMany().HasForeignKey(c => c.VideoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<VideoComment>().HasIndex(c => c.VideoId);

        // Podcast -> Article: xóa bài thì gỡ liên kết (không chặn xóa bài)
        mb.Entity<Podcast>()
            .HasOne(p => p.Article).WithMany().HasForeignKey(p => p.ArticleId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Podcast>().HasIndex(p => p.ArticleId);

        // Token xác nhận email
        mb.Entity<EmailConfirmationToken>().Property(t => t.Token).HasMaxLength(128);
        mb.Entity<EmailConfirmationToken>().HasIndex(t => t.Token).IsUnique();
        mb.Entity<EmailConfirmationToken>()
            .HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);



        // ===== Phân loại AI: ánh xạ danh mục + nhật ký sửa =====
        mb.Entity<CategoryMapping>().Property(m => m.AiLabel).HasMaxLength(150);
        mb.Entity<CategoryMapping>().HasIndex(m => m.AiLabel);
        mb.Entity<CategoryMapping>()
            .HasOne(m => m.Category).WithMany()
            .HasForeignKey(m => m.CategoryId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<AiCategoryCorrectionLog>().Property(l => l.EditorName).HasMaxLength(150);
        mb.Entity<AiCategoryCorrectionLog>().Property(l => l.OldCategory).HasMaxLength(150);
        mb.Entity<AiCategoryCorrectionLog>().Property(l => l.NewCategory).HasMaxLength(150);

        // ===== Đơn đặt quảng cáo (Advertisement gắn AdSlot) =====
        mb.Entity<Advertisement>().Property(a => a.Amount).HasColumnType("decimal(18,0)");
        mb.Entity<Advertisement>()
            .HasOne(a => a.AdSlot).WithMany()
            .HasForeignKey(a => a.AdSlotId).OnDelete(DeleteBehavior.SetNull);

        // ===== Slot quảng cáo (bán quảng cáo) =====
        mb.Entity<AdSlot>().Property(s => s.Name).HasMaxLength(120);
        mb.Entity<AdSlot>().Property(s => s.SlotKey).HasMaxLength(50);
        mb.Entity<AdSlot>().Property(s => s.Size).HasMaxLength(50);
        mb.Entity<AdSlot>().Property(s => s.PricePerDay).HasColumnType("decimal(18,0)");
        mb.Entity<AdSlot>().HasIndex(s => s.SlotKey).IsUnique();
        // SlotKey = ĐÚNG mã khu render (header/home_left/home_right/in_article/sidebar) để buy-flow,
        // hiển thị và preview dùng chung một bộ từ vựng, QC mua slot nào hiện đúng khu đó.
        mb.Entity<AdSlot>().HasData(
            new AdSlot { Id=1, Name="Banner Đầu Trang (ngang)", SlotKey="header",      Description="Banner ngang 728x90 nằm trên cùng, phía trên cả logo — mọi trang", Size="Banner728x90",      PricePerDay=500000, IsActive=true, CreatedAt=new DateTime(2025,1,1) },
            new AdSlot { Id=2, Name="Dải Dọc Trái",            SlotKey="home_left",   Description="Dải dọc 160x600 bên trái trang chủ",                                Size="Skyscraper160x600", PricePerDay=300000, IsActive=true, CreatedAt=new DateTime(2025,1,1) },
            new AdSlot { Id=3, Name="Dải Dọc Phải",            SlotKey="home_right",  Description="Dải dọc 160x600 bên phải trang chủ",                                Size="Skyscraper160x600", PricePerDay=300000, IsActive=true, CreatedAt=new DateTime(2025,1,1) },
            new AdSlot { Id=4, Name="Giữa Bài Viết",           SlotKey="in_article",  Description="Banner 728x90 chèn giữa nội dung bài viết",                          Size="Banner728x90",      PricePerDay=400000, IsActive=true, CreatedAt=new DateTime(2025,1,1) },
            new AdSlot { Id=5, Name="Cạnh Bài Viết",           SlotKey="sidebar",     Description="Quảng cáo 300x250 ở sidebar cạnh bài viết",                          Size="Rectangle300x250",  PricePerDay=250000, IsActive=true, CreatedAt=new DateTime(2025,1,1) }
        );

        // ===== Cấu hình khu hiển thị (chu kỳ nhảy QC) =====
        mb.Entity<AdZoneSetting>().Property(z => z.Position).HasMaxLength(50);
        mb.Entity<AdZoneSetting>().HasIndex(z => z.Position).IsUnique();
        mb.Entity<AdZoneSetting>().HasData(
            new AdZoneSetting { Id=1, Position="header",     RotationSeconds=5 },
            new AdZoneSetting { Id=2, Position="home_left",  RotationSeconds=5 },
            new AdZoneSetting { Id=3, Position="home_right", RotationSeconds=5 },
            new AdZoneSetting { Id=4, Position="in_article", RotationSeconds=5 },
            new AdZoneSetting { Id=5, Position="sidebar",    RotationSeconds=5 }
        );

        // ===== Giao dịch (đơn thanh toán quảng cáo) — tái dùng Transaction =====
        mb.Entity<Transaction>().Property(t => t.Amount).HasColumnType("decimal(18,2)");
        mb.Entity<Transaction>().Property(t => t.PaymentMethodLabel).HasMaxLength(50);
        mb.Entity<Transaction>().HasIndex(t => t.UserId);
        mb.Entity<Transaction>()
            .HasOne(t => t.Advertisement).WithMany()
            .HasForeignKey(t => t.AdvertisementId).OnDelete(DeleteBehavior.SetNull);

        // =====================================================================
        // CHUẨN HÓA DOMAIN — FK cho các bảng trước đây đứng lẻ (nullable + Restrict/SetNull,
        // tránh vòng lặp cascade). KHÔNG ảnh hưởng cụm Users/Articles/Chat đang ổn định.
        // =====================================================================

        // ---------- Domain ARTICLE: Video (1 Article - nhiều Video, tùy chọn) ----------
        mb.Entity<Video>()
            .HasOne(v => v.Article).WithMany(a => a.Videos)
            .HasForeignKey(v => v.ArticleId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Video>().HasIndex(v => v.ArticleId);

        // ---------- Domain USER ----------
        mb.Entity<SavedArticle>()
            .HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<SavedArticle>()
            .HasOne(s => s.Article).WithMany().HasForeignKey(s => s.ArticleId).OnDelete(DeleteBehavior.Cascade);

        mb.Entity<UserCategoryFollow>()
            .HasOne(f => f.User).WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<UserCategoryFollow>()
            .HasOne(f => f.Category).WithMany().HasForeignKey(f => f.CategoryId).OnDelete(DeleteBehavior.Cascade);

        // SearchHistory.UserId nullable -> SetNull (giữ lịch sử khi xóa user)
        mb.Entity<SearchHistory>()
            .HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.SetNull);


        // FeedbackReport.UserId nullable (khách vãng lai = null) -> SetNull
        mb.Entity<FeedbackReport>()
            .HasOne(f => f.User).WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.SetNull);

        // Notification: TargetUserId + RelatedArticleId (đều nullable) -> SetNull, FK-only cho gọn
        mb.Entity<Notification>()
            .HasOne<User>().WithMany().HasForeignKey(n => n.TargetUserId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Notification>()
            .HasOne<Article>().WithMany().HasForeignKey(n => n.RelatedArticleId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Notification>().HasIndex(n => n.TargetUserId);

        // ---------- Domain JOURNALIST / STAFF (1-1, FK-only theo convention hiện tại) ----------
        // JournalistProfile 1-1 User (unique index UserId đã khai ở trên)
        mb.Entity<JournalistProfile>()
            .HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        // StaffProfile 1-1 Admin (KHÔNG phải User) — unique index AdminId đã khai ở trên
        mb.Entity<StaffProfile>()
            .HasOne<Admin>().WithMany().HasForeignKey(p => p.AdminId).OnDelete(DeleteBehavior.Restrict);

        // ---------- Domain ADMIN / IMPORT ----------
        // RssSource gắn Category mặc định (cấu hình nguồn nhập) — nullable -> SetNull
        mb.Entity<RssSource>()
            .HasOne<Category>().WithMany().HasForeignKey(r => r.DefaultCategoryId).OnDelete(DeleteBehavior.SetNull);

        // StaffActivityLog: người thực hiện + hồ sơ bị tác động (đều nullable) -> SetNull
        mb.Entity<StaffActivityLog>()
            .HasOne<Admin>().WithMany().HasForeignKey(l => l.ActorAdminId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<StaffActivityLog>()
            .HasOne<Admin>().WithMany().HasForeignKey(l => l.TargetAdminId).OnDelete(DeleteBehavior.NoAction);
        // (SeedViewBatch: cố ý KHÔNG FK về Article — 1 đợt trải nhiều bài, đã lưu delta trong DetailsJson)


        // =====================================================================
        // NỐI NỐT — cột "người tạo / người chỉnh sửa" (ownership). Tất cả nullable -> SetNull.
        // "Admin có gì nối đó, Journalist/User có gì nối đó."
        // =====================================================================

        // AiCategoryCorrectionLog: bài bị sửa phân loại
        mb.Entity<AiCategoryCorrectionLog>()
            .HasOne(l => l.Article).WithMany().HasForeignKey(l => l.ArticleId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<AiCategoryCorrectionLog>().HasIndex(l => l.ArticleId);

        // Advertisement: người tạo là Admin hoặc Nhà báo (User)
        mb.Entity<Advertisement>()
            .HasOne<Admin>().WithMany().HasForeignKey(a => a.CreatedByAdminId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Advertisement>()
            .HasOne<User>().WithMany().HasForeignKey(a => a.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);

        // Video: người tạo là Admin hoặc User
        mb.Entity<Video>()
            .HasOne<Admin>().WithMany().HasForeignKey(v => v.CreatedByAdminId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Video>()
            .HasOne<User>().WithMany().HasForeignKey(v => v.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);

        // Notification: admin đã gửi thông báo
        mb.Entity<Notification>()
            .HasOne<Admin>().WithMany().HasForeignKey(n => n.SentByAdminId).OnDelete(DeleteBehavior.SetNull);

        // AiSetting / AutoImportSettings: admin chỉnh cấu hình gần nhất (audit)
        mb.Entity<AiSetting>()
            .HasOne<Admin>().WithMany().HasForeignKey(s => s.UpdatedByAdminId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<AutoImportSettings>()
            .HasOne<Admin>().WithMany().HasForeignKey(s => s.UpdatedByAdminId).OnDelete(DeleteBehavior.SetNull);
    }
}
