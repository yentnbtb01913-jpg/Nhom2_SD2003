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
    public DbSet<NewsletterSubscriber> NewsletterSubscribers { get; set; }

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
    public DbSet<AdRenewalMessage> AdRenewalMessages { get; set; }
    public DbSet<TeamChatMessage> TeamChatMessages { get; set; }
    public DbSet<NewsletterEmailLog> NewsletterEmailLogs { get; set; }
    public DbSet<Podcast> Podcasts { get; set; }
    public DbSet<EmailConfirmationToken> EmailConfirmationTokens { get; set; }
    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public DbSet<PlanFeature> PlanFeatures { get; set; }
    public DbSet<UserSubscription> UserSubscriptions { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<CustomerActivityLog> CustomerActivityLogs { get; set; }
    public DbSet<AutoImportSettings> AutoImportSettings { get; set; }
    public DbSet<CategoryMapping> CategoryMappings { get; set; }
    public DbSet<AiCategoryCorrectionLog> AiCategoryCorrectionLogs { get; set; }
    public DbSet<SearchHistory> SearchHistories { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
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
        mb.Entity<NewsletterSubscriber>().HasIndex(n => n.Email).IsUnique();
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
        mb.Entity<NewsletterEmailLog>().HasIndex(x => x.SubscriberId);

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

        // ===== Chat nội bộ ban quản lý =====
        mb.Entity<TeamChatMessage>().Property(m => m.ConversationKey).HasMaxLength(120);
        mb.Entity<TeamChatMessage>().Property(m => m.SenderRole).HasMaxLength(20);
        mb.Entity<TeamChatMessage>().Property(m => m.SenderName).HasMaxLength(150);
        mb.Entity<TeamChatMessage>().Property(m => m.RecipientRole).HasMaxLength(20);
        mb.Entity<TeamChatMessage>().HasIndex(m => m.ConversationKey);

        // ===== Audit log thao tác khách hàng (Quản lý khách hàng) =====
        mb.Entity<CustomerActivityLog>().Property(l => l.ActorRole).HasMaxLength(20);
        mb.Entity<CustomerActivityLog>().Property(l => l.ActorName).HasMaxLength(150);
        mb.Entity<CustomerActivityLog>().Property(l => l.Action).HasMaxLength(60);
        mb.Entity<CustomerActivityLog>().HasIndex(l => l.UserId);

        // ===== Phân loại AI: ánh xạ danh mục + nhật ký sửa =====
        mb.Entity<CategoryMapping>().Property(m => m.AiLabel).HasMaxLength(150);
        mb.Entity<CategoryMapping>().HasIndex(m => m.AiLabel);
        mb.Entity<CategoryMapping>()
            .HasOne(m => m.Category).WithMany()
            .HasForeignKey(m => m.CategoryId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<AiCategoryCorrectionLog>().Property(l => l.EditorName).HasMaxLength(150);
        mb.Entity<AiCategoryCorrectionLog>().Property(l => l.OldCategory).HasMaxLength(150);
        mb.Entity<AiCategoryCorrectionLog>().Property(l => l.NewCategory).HasMaxLength(150);

        // ===== Chat gia hạn quảng cáo =====
        mb.Entity<AdRenewalMessage>().Property(m => m.SenderRole).HasMaxLength(20);
        mb.Entity<AdRenewalMessage>().Property(m => m.SenderName).HasMaxLength(150);
        mb.Entity<AdRenewalMessage>().HasIndex(m => m.AdvertisementId);
        mb.Entity<AdRenewalMessage>()
            .HasOne(m => m.Advertisement).WithMany()
            .HasForeignKey(m => m.AdvertisementId).OnDelete(DeleteBehavior.Cascade);

        // ===== Gói Premium =====
        mb.Entity<SubscriptionPlan>().Property(p => p.Name).HasMaxLength(120);
        mb.Entity<SubscriptionPlan>().Property(p => p.Description).HasMaxLength(500);
        mb.Entity<SubscriptionPlan>().Property(p => p.Price).HasColumnType("decimal(18,2)");

        mb.Entity<PlanFeature>().Property(f => f.FeatureText).HasMaxLength(200);
        mb.Entity<PlanFeature>()
            .HasOne(f => f.Plan).WithMany(p => p.Features)
            .HasForeignKey(f => f.PlanId).OnDelete(DeleteBehavior.Cascade);

        mb.Entity<UserSubscription>().Property(s => s.Notes).HasMaxLength(500);
        mb.Entity<UserSubscription>().HasIndex(s => s.UserId);
        mb.Entity<UserSubscription>()
            .HasOne(s => s.Plan).WithMany()
            .HasForeignKey(s => s.PlanId).OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Transaction>().Property(t => t.Amount).HasColumnType("decimal(18,2)");
        mb.Entity<Transaction>().Property(t => t.PaymentMethodLabel).HasMaxLength(50);
        mb.Entity<Transaction>().HasIndex(t => t.UserId);
        mb.Entity<Transaction>()
            .HasOne(t => t.Plan).WithMany()
            .HasForeignKey(t => t.PlanId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<Transaction>()
            .HasOne<UserSubscription>().WithMany()
            .HasForeignKey(t => t.UserSubscriptionId).OnDelete(DeleteBehavior.SetNull);
    }
}
