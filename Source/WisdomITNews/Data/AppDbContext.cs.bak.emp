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

        mb.Entity<Category>()
            .HasOne(c => c.ParentCategory).WithMany(c => c.Children).HasForeignKey(c => c.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);

        // Indexes
        mb.Entity<Article>().HasIndex(a => a.Slug).IsUnique();
        mb.Entity<Article>().HasIndex(a => a.Status);
        mb.Entity<Article>().HasIndex(a => a.Region);
        mb.Entity<Category>().HasIndex(c => c.Slug).IsUnique();
        mb.Entity<Tag>().HasIndex(t => t.Slug).IsUnique();
        mb.Entity<Admin>().HasIndex(a => a.Username).IsUnique();
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

        // Bình luận video — FK tới Video (xóa video -> xóa bình luận). KHÔNG cấu hình self-FK cho ParentId (giữ phẳng, tránh đệ quy).
        mb.Entity<VideoComment>()
            .HasOne(c => c.Video).WithMany().HasForeignKey(c => c.VideoId).OnDelete(DeleteBehavior.Cascade);
        mb.Entity<VideoComment>().HasIndex(c => c.VideoId);
    }
}
