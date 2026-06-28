using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WisdomITNews.Models;

public class Admin
{
    public int Id { get; set; }
    [Required] public string Username { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    [Required] public string FullName { get; set; } = "";
    [Required] public string Email { get; set; } = "";
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public string Role { get; set; } = "editor";
    public bool IsActive { get; set; } = true;
    public DateTime? LastLogin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Article> Articles { get; set; } = new List<Article>();
}

public class Category
{
    public int Id { get; set; }
    [Required] public string Name { get; set; } = "";
    [Required] public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string Color { get; set; } = "#e63946";
    public int SortOrder { get; set; } = 0;
    public bool IsVisible { get; set; } = true;
    public int? ParentCategoryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public Category? ParentCategory { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<Article> Articles { get; set; } = new List<Article>();

    [NotMapped] public int ArticleCount { get; set; }
}

public class Article
{
    public int Id { get; set; }
    [Required] public string Title { get; set; } = "";
    [Required] public string Slug { get; set; } = "";
    [Required] public string Summary { get; set; } = "";
    [Required] public string Content { get; set; } = "";
    public string? Thumbnail { get; set; }
    public string? ThumbnailAlt { get; set; }
    public int? CategoryId { get; set; }
    public int? AuthorId { get; set; }
    public int Views { get; set; } = 0;
    public string Status { get; set; } = "draft";
    public bool IsFeatured { get; set; } = false;
    public bool IsBreaking { get; set; } = false;
    public DateTime? PublishedAt { get; set; }
    public string? AiSummary { get; set; }
    public string? SourceName { get; set; }   // nguồn (vd: The Hacker News)
    public string? SourceUrl { get; set; }     // link bài gốc
    public bool IsExternal { get; set; } = false; // true = bài tổng hợp từ nguồn ngoài
    public string? MetaTitle { get; set; }
    public string? MetaDesc { get; set; }
    public string? Region { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? AuthorUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public Category? Category { get; set; }
    public Admin? Author { get; set; }
    public User? AuthorUser { get; set; }
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<ArticleTag> ArticleTags { get; set; } = new List<ArticleTag>();
    public ICollection<AILog> AILogs { get; set; } = new List<AILog>();
    public ICollection<ViewHistory> ViewHistories { get; set; } = new List<ViewHistory>();
}

public class Tag
{
    public int Id { get; set; }
    [Required] public string Name { get; set; } = "";
    [Required] public string Slug { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<ArticleTag> ArticleTags { get; set; } = new List<ArticleTag>();
}

public class ArticleTag
{
    public int ArticleId { get; set; }
    public int TagId { get; set; }
    public Article? Article { get; set; }
    public Tag? Tag { get; set; }
}

public class Comment
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    [Required] public string AuthorName { get; set; } = "";
    public string? AuthorEmail { get; set; }
    [Required] public string Content { get; set; } = "";
    public int? ParentId { get; set; }
    public int Likes { get; set; } = 0;
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int? ParentCommentId { get; set; }
    public int LikeCount { get; set; } = 0;
    public int DislikeCount { get; set; } = 0;
    public int? UserId { get; set; }

    public Article? Article { get; set; }
    public Comment? ParentComment { get; set; }
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();
    public User? User { get; set; }
    public ICollection<CommentVote> Votes { get; set; } = new List<CommentVote>();
}

public class AILog
{
    public long Id { get; set; }
    public int? ArticleId { get; set; }
    public string Action { get; set; } = "";
    public string? PromptText { get; set; }
    public string? ResultText { get; set; }
    public string? ModelUsed { get; set; }
    public int TokensUsed { get; set; } = 0;
    public bool IsSuccess { get; set; } = true;
    public string? ErrorMsg { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Article? Article { get; set; }
}

public class NewsletterSubscriber
{
    public int Id { get; set; }
    [Required] public string Email { get; set; } = "";
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Source { get; set; }
    public string Status { get; set; } = "active";
    public DateTime SubscribedAt { get; set; } = DateTime.Now;
}

public class CommentVote
{
    public int Id { get; set; }
    public int CommentId { get; set; }
    [Required] public string SessionId { get; set; } = "";
    [Required] public string VoteType { get; set; } = "Like";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Comment? Comment { get; set; }
}

public class ViewHistory
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    [Required] public string SessionId { get; set; } = "";
    public DateTime ViewedAt { get; set; } = DateTime.Now;

    public Article? Article { get; set; }
}

public class FeedbackReport
{
    public int Id { get; set; }
    public string? PageUrl { get; set; }
    [Required] public string Type { get; set; } = "other";
    [Required] public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsResolved { get; set; } = false;
}

public class User
{
    public int Id { get; set; }
    [Required] public string Username { get; set; } = "";
    [Required] public string Email { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    [Required] public string FullName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public string? CoverUrl { get; set; }
    public string? Bio { get; set; }
    public string Role { get; set; } = "Reader";
    public bool IsActive { get; set; } = true;
    public bool IsEmailConfirmed { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    public ICollection<Article> Articles { get; set; } = new List<Article>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
}

public class RssSource
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string FeedUrl { get; set; } = "";
    public string? Description { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Country { get; set; }
    public bool IsActive { get; set; } = true;
    public int? DefaultCategoryId { get; set; }
    public int MaxImport { get; set; } = 30;
    public DateTime? LastImportAt { get; set; }
    public int TotalImported { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class Notification
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Type { get; set; } = "system";
    public string Icon { get; set; } = "bell";
    public string IconColor { get; set; } = "#159aa3";
    
    public string TargetType { get; set; } = "all";
    public int? TargetUserId { get; set; }
    public string? TargetEmail { get; set; }
    public string? TargetRole { get; set; }
    
    public string? ViolationContent { get; set; }
    public string? ViolationReason { get; set; }
    public int? RelatedArticleId { get; set; }
    public int? RelatedCommentId { get; set; }
    public int? RelatedVideoId { get; set; }
    
    public bool IsRead { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    
    public string SentBy { get; set; } = "system";
    public int? SentByAdminId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ReadAt { get; set; }
}
