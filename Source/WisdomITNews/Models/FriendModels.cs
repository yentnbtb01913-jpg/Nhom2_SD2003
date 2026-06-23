using System.ComponentModel.DataAnnotations;

namespace WisdomITNews.Models;

/// <summary>
/// Kết bạn kiểu Facebook: gửi yêu cầu → chấp nhận/từ chối
/// </summary>
public class Friendship
{
    public int Id { get; set; }
    public int RequesterId { get; set; }    // Người gửi lời mời
    public int ReceiverId { get; set; }     // Người nhận lời mời
    public string Status { get; set; } = "pending"; // pending, accepted, rejected
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? AcceptedAt { get; set; }

    public User? Requester { get; set; }
    public User? Receiver { get; set; }
}

/// <summary>
/// Theo dõi kiểu Twitter: không cần chấp nhận
/// </summary>
public class UserFollow
{
    public int Id { get; set; }
    public int FollowerId { get; set; }     // Người theo dõi
    public int FollowingId { get; set; }    // Người được theo dõi
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public User? Follower { get; set; }
    public User? Following { get; set; }
}