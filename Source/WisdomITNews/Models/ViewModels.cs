using System.ComponentModel.DataAnnotations;
namespace WisdomITNews.Models;

public class HomeViewModel
{
    public List<Article> FeaturedArticles { get; set; } = new();
    public List<Article> LatestArticles { get; set; } = new();
    public List<Article> AIArticles { get; set; } = new();
    public List<Article> PopularArticles { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();
}

public class ArticleViewModel
{
    public Article Article { get; set; } = new();
    public List<Comment> Comments { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();
    public List<Article> RelatedArticles { get; set; } = new();
    public List<Article> PopularArticles { get; set; } = new();
}

public class CategoryViewModel
{
    public Category? Category { get; set; }
    public List<Article> Articles { get; set; } = new();
    public List<Category> AllCategories { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class SearchViewModel
{
    public string Keyword { get; set; } = "";
    public List<Article> Results { get; set; } = new();
    public int TotalCount { get; set; }
}

public class DashboardViewModel
{
    public int TotalArticles { get; set; }
    public int PublishedArticles { get; set; }
    public int TotalViews { get; set; }
    public int TotalComments { get; set; }
    public int PendingComments { get; set; }
    public int Subscribers { get; set; }
    public List<Article> RecentArticles { get; set; } = new();
    public List<Article> TopArticles { get; set; } = new();
    // Chat stats
    public int TotalChatGroups { get; set; }
    public int TotalChatMessages { get; set; }
    public int TotalChatUsers { get; set; }
    public List<ChatGroup> RecentChatGroups { get; set; } = new();
}

public class ArticleFormViewModel
{
    public Article Article { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public string Tags { get; set; } = "";
    public string Error { get; set; } = "";
    public string Success { get; set; } = "";
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public T? Data { get; set; }
}

public class SummarizeResponse
{
    public bool Success { get; set; }
    public string Summary { get; set; } = "";
    public int Tokens { get; set; }
    public bool Cached { get; set; }
}

public class TitleSuggestion
{
    public string Style { get; set; } = "";
    public string Text { get; set; } = "";
}

public class SuggestTitleResponse
{
    public bool Success { get; set; }
    public List<TitleSuggestion> Titles { get; set; } = new();
    public int Tokens { get; set; }
}

public class CommentRequest
{
    public int ArticleId { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string Content { get; set; } = "";
    public int? ParentCommentId { get; set; }
}

public class ReplyRequest
{
    public int ArticleId { get; set; }
    public int ParentCommentId { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string Content { get; set; } = "";
}

public class NewsletterRequest
{
    public string Email { get; set; } = "";
}

public class ViewHistoryItem
{
    public ViewHistory History { get; set; } = new();
    public Article Article { get; set; } = new();
}

public class WeatherViewModel
{
    public string City { get; set; } = "";
    public double Temp { get; set; }
    public string Description { get; set; } = "";
    public string IconCode { get; set; } = "01d";
}

public class RegionViewModel
{
    public string RegionSlug { get; set; } = "";
    public string RegionName { get; set; } = "";
    public double CenterLat { get; set; }
    public double CenterLng { get; set; }
    public List<Article> Articles { get; set; } = new();
    public List<Article> FeaturedArticles { get; set; } = new();
}

public class ModerationResult
{
    public int Score { get; set; }
    public List<string> Issues { get; set; } = new();
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "Họ tên từ 2 đến 60 ký tự")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "Tên đăng nhập từ 3 đến 30 ký tự")]
    [RegularExpression(@"^[a-zA-Z0-9_.\-]+$", ErrorMessage = "Tên đăng nhập chỉ gồm chữ, số và . _ -")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(120, ErrorMessage = "Email tối đa 120 ký tự")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Mật khẩu phải có cả chữ và số")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu")]
    [Compare("Password", ErrorMessage = "Mật khẩu nhập lại không khớp")]
    public string ConfirmPassword { get; set; } = "";

    public string Error { get; set; } = "";
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc email")]
    public string UsernameOrEmail { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    public string Password { get; set; } = "";

    public string Error { get; set; } = "";
}

public class ProfileViewModel
{
    public User User { get; set; } = new();
    public List<Article> Articles { get; set; } = new();
    public int CommentCount { get; set; }

    // Friend/Follow
    public string FriendStatus { get; set; } = "none"; // none, friends, sent, received
    public bool IFollow { get; set; } = false;
    public bool FollowsMe { get; set; } = false;
    public int FriendCount { get; set; }
    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }
    public bool IsOwnProfile { get; set; } = false;
}

public class JournalistDashboardViewModel
{
    public User User { get; set; } = new();
    public List<Article> Articles { get; set; } = new();
    public int TotalViews { get; set; }
    public int TotalComments { get; set; }
    public int PublishedCount { get; set; }
    public int DraftCount { get; set; }
    public int PendingCount { get; set; }
}
public class JournalistRegisterViewModel
{
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Bio { get; set; }
    public string Error { get; set; } = "";
}

public class JournalistLoginViewModel
{
    public string UsernameOrEmail { get; set; } = "";
    public string Password { get; set; } = "";
    public string Error { get; set; } = "";
}