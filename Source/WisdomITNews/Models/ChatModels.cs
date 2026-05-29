using System.ComponentModel.DataAnnotations;

namespace WisdomITNews.Models;

public class ChatGroup
{
    public int Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public string? Avatar { get; set; }
    public string CreatorType { get; set; } = "user";
    public int CreatorId { get; set; }
    public bool IsDirectMessage { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<ChatMember> Members { get; set; } = new List<ChatMember>();
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public class ChatMember
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string MemberType { get; set; } = "user";
    public int MemberId { get; set; }
    public string Role { get; set; } = "member";
    public DateTime JoinedAt { get; set; } = DateTime.Now;

    public ChatGroup? Group { get; set; }
}

public class ChatMessage
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string SenderType { get; set; } = "user";
    public int SenderId { get; set; }
    public string SenderName { get; set; } = "";
    public string? SenderAvatar { get; set; }
    [Required] public string Content { get; set; } = "";
    public string MessageType { get; set; } = "text";
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public int? ReplyToId { get; set; }
    public bool IsPinned { get; set; } = false;
    public DateTime SentAt { get; set; } = DateTime.Now;

    public ChatGroup? Group { get; set; }
    public ChatMessage? ReplyTo { get; set; }
}