# WisdomITNews - Class Diagram

## Domain Models (Entity Framework Core)

### Core Content Models

```
┌─────────────────────────────────────────────────────────────────────┐
│                            Article                                   │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + Title: string                                                     │
│ + Slug: string                                                      │
│ + Summary: string                                                   │
│ + Content: string                                                   │
│ + Thumbnail: string?                                                │
│ + ThumbnailAlt: string?                                             │
│ + CategoryId: int?                                                  │
│ + AuthorId: int?                                                    │
│ + AuthorUserId: int?                                                 │
│ + Views: int                                                        │
│ + Status: string                                                    │
│ + IsFeatured: bool                                                  │
│ + IsBreaking: bool                                                   │
│ + PublishedAt: DateTime?                                            │
│ + AiSummary: string?                                                │
│ + SourceName: string?                                                │
│ + SourceUrl: string?                                                │
│ + IsExternal: bool                                                  │
│ + MetaTitle: string?                                                │
│ + MetaDesc: string?                                                 │
│ + Region: string?                                                   │
│ + Latitude: double?                                                │
│ + Longitude: double?                                                │
│ + CreatedAt: DateTime                                               │
│ + UpdatedAt: DateTime                                               │
├─────────────────────────────────────────────────────────────────────┤
│ - Category: Category?                                                │
│ - Author: Admin?                                                    │
│ - AuthorUser: User?                                                 │
│ + Comments: ICollection<Comment>                                    │
│ + ArticleTags: ICollection<ArticleTag>                              │
│ + AILogs: ICollection<AILog>                                        │
│ + ViewHistories: ICollection<ViewHistory>                           │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ 1
                                    │ *
                                    │
┌─────────────────────────────────────────────────────────────────────┐
│                           Category                                   │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + Name: string                                                      │
│ + Slug: string                                                      │
│ + Description: string?                                              │
│ + Icon: string?                                                     │
│ + Color: string                                                     │
│ + SortOrder: int                                                    │
│ + IsVisible: bool                                                   │
│ + ParentCategoryId: int?                                            │
│ + CreatedAt: DateTime                                               │
│ + UpdatedAt: DateTime                                               │
│ + ArticleCount: int [NotMapped]                                     │
├─────────────────────────────────────────────────────────────────────┤
│ - ParentCategory: Category?                                         │
│ + Children: ICollection<Category>                                   │
│ + Articles: ICollection<Article>                                   │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ 1 (self-reference)
                                    │ *
                                    │
                            ┌──────────┐
                            │ Category │
                            └──────────┘

```

```
┌─────────────────────────────────────────────────────────────────────┐
│                              Tag                                      │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + Name: string                                                      │
│ + Slug: string                                                      │
│ + CreatedAt: DateTime                                               │
├─────────────────────────────────────────────────────────────────────┤
│ + ArticleTags: ICollection<ArticleTag>                              │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ *
                                    │
┌─────────────────────────────────────────────────────────────────────┐
│                           ArticleTag                                  │
├─────────────────────────────────────────────────────────────────────┤
│ + ArticleId: int [PK]                                                │
│ + TagId: int [PK]                                                    │
├─────────────────────────────────────────────────────────────────────┤
│ - Article: Article?                                                 │
│ - Tag: Tag?                                                         │
└─────────────────────────────────────────────────────────────────────┘
```

### User & Authentication Models

```
┌─────────────────────────────────────────────────────────────────────┐
│                              Admin                                    │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + Username: string                                                  │
│ + PasswordHash: string                                              │
│ + FullName: string                                                  │
│ + Email: string                                                     │
│ + Gender: string?                                                   │
│ + Address: string?                                                  │
│ + Role: string                                                      │
│ + IsActive: bool                                                    │
│ + LastLogin: DateTime?                                              │
│ + CreatedAt: DateTime                                               │
├─────────────────────────────────────────────────────────────────────┤
│ + Articles: ICollection<Article>                                    │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                               User                                   │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + Username: string                                                  │
│ + Email: string                                                     │
│ + PasswordHash: string                                              │
│ + FullName: string                                                  │
│ + AvatarUrl: string?                                                 │
│ + CoverUrl: string?                                                 │
│ + Bio: string?                                                      │
│ + Role: string                                                      │
│ + IsActive: bool                                                    │
│ + IsEmailConfirmed: bool                                            │
│ + CreatedAt: DateTime                                               │
├─────────────────────────────────────────────────────────────────────┤
│ + Articles: ICollection<Article>                                    │
│ + Comments: ICollection<Comment>                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Comment System

```
┌─────────────────────────────────────────────────────────────────────┐
│                            Comment                                   │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + ArticleId: int                                                    │
│ + AuthorName: string                                                │
│ + AuthorEmail: string?                                              │
│ + Content: string                                                   │
│ + ParentId: int?                                                    │
│ + Likes: int                                                        │
│ + Status: string                                                    │
│ + CreatedAt: DateTime                                               │
│ + ParentCommentId: int?                                             │
│ + LikeCount: int                                                    │
│ + DislikeCount: int                                                 │
│ + UserId: int?                                                      │
├─────────────────────────────────────────────────────────────────────┤
│ - Article: Article?                                                 │
│ - ParentComment: Comment?                                           │
│ + Replies: ICollection<Comment>                                      │
│ - User: User?                                                       │
│ + Votes: ICollection<CommentVote>                                   │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ 1 (self-reference)
                                    │ *
                                    │
                            ┌──────────┐
                            │ Comment  │
                            └──────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                          CommentVote                                 │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + CommentId: int                                                    │
│ + SessionId: string                                                  │
│ + VoteType: string                                                  │
│ + CreatedAt: DateTime                                               │
├─────────────────────────────────────────────────────────────────────┤
│ - Comment: Comment?                                                 │
└─────────────────────────────────────────────────────────────────────┘
```

### Chat System

```
┌─────────────────────────────────────────────────────────────────────┐
│                           ChatGroup                                  │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + Name: string                                                      │
│ + Avatar: string?                                                   │
│ + CreatorType: string                                               │
│ + CreatorId: int                                                    │
│ + IsDirectMessage: bool                                             │
│ + CreatedAt: DateTime                                               │
├─────────────────────────────────────────────────────────────────────┤
│ + Members: ICollection<ChatMember>                                  │
│ + Messages: ICollection<ChatMessage>                                │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ 1
                                    │ *
                                    │
┌─────────────────────────────────────────────────────────────────────┐
│                           ChatMember                                 │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + GroupId: int                                                      │
│ + MemberType: string                                                │
│ + MemberId: int                                                     │
│ + Role: string                                                      │
│ + JoinedAt: DateTime                                                │
├─────────────────────────────────────────────────────────────────────┤
│ - Group: ChatGroup?                                                 │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                          ChatMessage                                 │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + GroupId: int                                                      │
│ + SenderType: string                                                │
│ + SenderId: int                                                     │
│ + SenderName: string                                                │
│ + SenderAvatar: string?                                             │
│ + Content: string                                                   │
│ + MessageType: string                                               │
│ + FileUrl: string?                                                  │
│ + FileName: string?                                                 │
│ + ReplyToId: int?                                                   │
│ + IsPinned: bool                                                    │
│ + SentAt: DateTime                                                  │
├─────────────────────────────────────────────────────────────────────┤
│ - Group: ChatGroup?                                                 │
│ - ReplyTo: ChatMessage?                                             │
└─────────────────────────────────────────────────────────────────────┘
```

### Social Features

```
┌─────────────────────────────────────────────────────────────────────┐
│                           Friendship                                 │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + RequesterId: int                                                  │
│ + ReceiverId: int                                                   │
│ + Status: string (pending/accepted/rejected)                         │
│ + CreatedAt: DateTime                                               │
│ + AcceptedAt: DateTime?                                             │
├─────────────────────────────────────────────────────────────────────┤
│ - Requester: User?                                                  │
│ - Receiver: User?                                                   │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                            UserFollow                                │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + FollowerId: int                                                   │
│ + FollowingId: int                                                  │
│ + CreatedAt: DateTime                                               │
├─────────────────────────────────────────────────────────────────────┤
│ - Follower: User?                                                   │
│ - Following: User?                                                  │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                          SavedArticle                                │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + UserId: int                                                       │
│ + ArticleId: int                                                    │
│ + SavedAt: DateTime                                                 │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                       UserCategoryFollow                             │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + UserId: int                                                       │
│ + CategoryId: int                                                    │
│ + CreatedAt: DateTime                                               │
└─────────────────────────────────────────────────────────────────────┘
```

### Video & RSS

```
┌─────────────────────────────────────────────────────────────────────┐
│                              Video                                   │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + Title: string                                                     │
│ + YouTubeId: string                                                  │
│ + Source: string?                                                   │
│ + Description: string?                                              │
│ + Status: string                                                    │
│ + Views: int                                                        │
│ + CreatedByAdminId: int?                                            │
│ + CreatedByUserId: int?                                             │
│ + VideoUrl: string?                                                 │
│ + VideoType: string                                                 │
│ + FileSize: long?                                                   │
│ + CreatedAt: DateTime                                               │
│ + PublishedAt: DateTime                                             │
├─────────────────────────────────────────────────────────────────────┤
│ + IsUpload: bool [computed]                                         │
│ + Thumbnail: string [computed]                                     │
│ + EmbedUrl: string [computed]                                       │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                            RssSource                                 │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + Name: string                                                      │
│ + FeedUrl: string                                                   │
│ + Description: string?                                              │
│ + WebsiteUrl: string?                                               │
│ + Country: string?                                                  │
│ + IsActive: bool                                                    │
│ + DefaultCategoryId: int?                                           │
│ + MaxImport: int                                                    │
│ + LastImportAt: DateTime?                                           │
│ + TotalImported: int                                                │
│ + CreatedAt: DateTime                                               │
└─────────────────────────────────────────────────────────────────────┘
```

### Supporting Models

```
┌─────────────────────────────────────────────────────────────────────┐
│                              AILog                                   │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: long                                                          │
│ + ArticleId: int?                                                   │
│ + Action: string                                                    │
│ + PromptText: string?                                               │
│ + ResultText: string?                                               │
│ + ModelUsed: string?                                                │
│ + TokensUsed: int                                                   │
│ + IsSuccess: bool                                                   │
│ + ErrorMsg: string?                                                 │
│ + CreatedAt: DateTime                                               │
├─────────────────────────────────────────────────────────────────────┤
│ - Article: Article?                                                 │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                        ViewHistory                                   │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + ArticleId: int                                                    │
│ + SessionId: string                                                  │
│ + ViewedAt: DateTime                                                │
├─────────────────────────────────────────────────────────────────────┤
│ - Article: Article?                                                 │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                      NewsletterSubscriber                            │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + Email: string                                                     │
│ + FullName: string?                                                 │
│ + Phone: string?                                                    │
│ + Source: string?                                                   │
│ + Status: string                                                    │
│ + SubscribedAt: DateTime                                            │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                        FeedbackReport                               │
├─────────────────────────────────────────────────────────────────────┤
│ + Id: int                                                           │
│ + PageUrl: string?                                                  │
│ + Type: string                                                      │
│ + Description: string                                                │
│ + CreatedAt: DateTime                                               │
│ + IsResolved: bool                                                  │
└─────────────────────────────────────────────────────────────────────┘
```

## Controllers (MVC Pattern)

```
┌─────────────────────────────────────────────────────────────────────┐
│                         HomeController                               │
├─────────────────────────────────────────────────────────────────────┤
│ - _db: AppDbContext                                                │
│ - _logger: ILogger<HomeController>                                  │
├─────────────────────────────────────────────────────────────────────┤
│ + Index() : Task<IActionResult>                                     │
│ + Category(string slug) : Task<IActionResult>                       │
│ + Search(string q) : Task<IActionResult>                            │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                       ArticleController                              │
├─────────────────────────────────────────────────────────────────────┤
│ - _db: AppDbContext                                                │
│ - _ai: AIService                                                    │
│ - _logger: ILogger<ArticleController>                              │
├─────────────────────────────────────────────────────────────────────┤
│ + Detail(string slug) : Task<IActionResult>                         │
│ + Chat([FromBody] ChatRequest) : Task<IActionResult>                │
│ + LikeComment(int id) : Task<IActionResult>                         │
│ + DislikeComment(int id) : Task<IActionResult>                       │
│ + ReplyComment() : Task<IActionResult>                              │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                        AdminController                               │
├─────────────────────────────────────────────────────────────────────┤
│ - _db: AppDbContext                                                │
│ - _imageUpload: ImageUploadService                                  │
│ - _ai: AIService                                                    │
│ - _email: EmailService                                              │
│ - _videoUpload: VideoUploadService                                  │
│ - _logger: ILogger<AdminController>                                │
├─────────────────────────────────────────────────────────────────────┤
│ + Login() : IActionResult                                           │
│ + Logout() : IActionResult                                          │
│ + Dashboard() : Task<IActionResult>                                 │
│ + Articles() : Task<IActionResult>                                 │
│ + CreateArticle() : Task<IActionResult>                             │
│ + EditArticle(int id) : Task<IActionResult>                         │
│ + DeleteArticle(int id) : Task<IActionResult>                        │
│ + Categories() : Task<IActionResult>                                │
│ + Comments() : Task<IActionResult>                                 │
│ + Subscribers() : Task<IActionResult>                                │
│ + Feedback() : Task<IActionResult>                                  │
│ + Videos() : Task<IActionResult>                                    │
│ + RssSources() : Task<IActionResult>                                │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                      NhanVienController                              │
├─────────────────────────────────────────────────────────────────────┤
│ - _db: AppDbContext                                                │
│ - _imageUpload: ImageUploadService                                  │
│ - _ai: AIService                                                    │
│ - _logger: ILogger<NhanVienController>                              │
├─────────────────────────────────────────────────────────────────────┤
│ + Login() : IActionResult                                           │
│ + Logout() : IActionResult                                          │
│ + Dashboard() : Task<IActionResult>                                 │
│ + Articles() : Task<IActionResult>                                 │
│ + CreateArticle() : Task<IActionResult>                             │
│ + EditArticle(int id) : Task<IActionResult>                         │
│ + Comments() : Task<IActionResult>                                 │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                    JournalistController                              │
├─────────────────────────────────────────────────────────────────────┤
│ - _db: AppDbContext                                                │
│ - _imageUpload: ImageUploadService                                  │
│ - _logger: ILogger<JournalistController>                            │
├─────────────────────────────────────────────────────────────────────┤
│ + Register() : IActionResult                                       │
│ + Login() : IActionResult                                           │
│ + Logout() : IActionResult                                          │
│ + Dashboard() : Task<IActionResult>                                 │
│ + CreateArticle() : Task<IActionResult>                             │
│ + EditArticle(int id) : Task<IActionResult>                         │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                      AccountController                               │
├─────────────────────────────────────────────────────────────────────┤
│ - _db: AppDbContext                                                │
│ - _logger: ILogger<AccountController>                              │
├─────────────────────────────────────────────────────────────────────┤
│ + Register() : IActionResult                                       │
│ + Login() : IActionResult                                           │
│ + Logout() : IActionResult                                          │
│ + Profile(string username) : Task<IActionResult>                    │
│ + EditProfile() : Task<IActionResult>                               │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                        ChatController                                │
├─────────────────────────────────────────────────────────────────────┤
│ - _db: AppDbContext                                                │
│ - _logger: ILogger<ChatController>                                  │
├─────────────────────────────────────────────────────────────────────┤
│ + Index() : Task<IActionResult>                                     │
│ + Group(int id) : Task<IActionResult>                               │
│ + CreateGroup() : Task<IActionResult>                               │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                      FriendController                               │
├─────────────────────────────────────────────────────────────────────┤
│ - _db: AppDbContext                                                │
│ - _logger: ILogger<FriendController>                               │
├─────────────────────────────────────────────────────────────────────┤
│ + SendRequest(int userId) : Task<IActionResult>                     │
│ + AcceptRequest(int id) : Task<IActionResult>                       │
│ + RejectRequest(int id) : Task<IActionResult>                       │
│ + Follow(int userId) : Task<IActionResult>                           │
│ + Unfollow(int userId) : Task<IActionResult>                         │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                    FeedbackController                               │
├─────────────────────────────────────────────────────────────────────┤
│ - _db: AppDbContext                                                │
│ - _logger: ILogger<FeedbackController>                             │
├─────────────────────────────────────────────────────────────────────┤
│ + Submit() : Task<IActionResult>                                    │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                       VideoController                               │
├─────────────────────────────────────────────────────────────────────┤
│ - _db: AppDbContext                                                │
│ - _logger: ILogger<VideoController>                                │
├─────────────────────────────────────────────────────────────────────┤
│ + Index() : Task<IActionResult>                                     │
│ + Detail(int id) : Task<IActionResult>                               │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                       RegionController                               │
├─────────────────────────────────────────────────────────────────────┤
│ - _db: AppDbContext                                                │
│ - _logger: ILogger<RegionController>                              │
├─────────────────────────────────────────────────────────────────────┤
│ + Index(string region) : Task<IActionResult>                         │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                        RssController                                 │
├─────────────────────────────────────────────────────────────────────┤
│ - _db: AppDbContext                                                │
│ - _importService: NewsImportService                                 │
│ - _logger: ILogger<RssController>                                  │
├─────────────────────────────────────────────────────────────────────┤
│ + ImportFromSource(int id) : Task<IActionResult>                    │
│ + ImportAll() : Task<IActionResult>                                 │
└─────────────────────────────────────────────────────────────────────┘
```

## Services

```
┌─────────────────────────────────────────────────────────────────────┐
│                           AIService                                  │
├─────────────────────────────────────────────────────────────────────┤
│ - _httpClient: HttpClient                                           │
│ - _apiKey: string                                                   │
│ - _logger: ILogger<AIService>                                      │
├─────────────────────────────────────────────────────────────────────┤
│ + SummarizeAsync(string content) : Task<SummarizeResponse>          │
│ + SuggestTitleAsync(string content) : Task<SuggestTitleResponse>    │
│ + ChatAsync(string message) : Task<(string reply, bool success)>   │
│ + ModerateAsync(string content) : Task<ModerationResult>            │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                       EmailService                                   │
├─────────────────────────────────────────────────────────────────────┤
│ - _smtpHost: string                                                  │
│ - _smtpPort: int                                                    │
│ - _smtpUser: string                                                  │
│ - _smtpPass: string                                                  │
│ - _logger: ILogger<EmailService>                                   │
├─────────────────────────────────────────────────────────────────────┤
│ + SendEmailAsync(string to, string subject, string body) : Task     │
│ + SendNewsletterAsync(List<string> recipients, string subject,      │
│   string body) : Task                                               │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                    ImageUploadService                                │
├─────────────────────────────────────────────────────────────────────┤
│ - _webHostEnvironment: IWebHostEnvironment                          │
│ - _logger: ILogger<ImageUploadService>                             │
├─────────────────────────────────────────────────────────────────────┤
│ + UploadAsync(IFormFile file) : Task<string>                        │
│ + DeleteAsync(string filePath) : Task                              │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                    VideoUploadService                                │
├─────────────────────────────────────────────────────────────────────┤
│ - _webHostEnvironment: IWebHostEnvironment                          │
│ - _logger: ILogger<VideoUploadService>                             │
├─────────────────────────────────────────────────────────────────────┤
│ + UploadAsync(IFormFile file) : Task<string>                        │
│ + DeleteAsync(string filePath) : Task                              │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                    NewsImportService                                │
├─────────────────────────────────────────────────────────────────────┤
│ - _httpClient: HttpClient                                           │
│ - _db: AppDbContext                                                │
│ - _logger: ILogger<NewsImportService>                              │
├─────────────────────────────────────────────────────────────────────┤
│ + ImportRssAsync(string feedUrl) : Task<int>                       │
│ + ImportFromSourceAsync(RssSource source) : Task<int>              │
│ - GetThumbnail(XElement item) : string?                             │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                       WeatherService                                 │
├─────────────────────────────────────────────────────────────────────┤
│ - _httpClient: HttpClient                                           │
│ - _apiKey: string                                                   │
│ - _logger: ILogger<WeatherService>                                │
├─────────────────────────────────────────────────────────────────────┤
│ + GetWeatherAsync(string city) : Task<WeatherViewModel>            │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                        SlugHelper                                   │
├─────────────────────────────────────────────────────────────────────┤
│ + Generate(string text) : string                                    │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                        SimpleXlsx                                   │
├─────────────────────────────────────────────────────────────────────┤
│ + ExportToExcel<T>(IEnumerable<T> data, string fileName) : byte[]  │
└─────────────────────────────────────────────────────────────────────┘
```

## Data Layer

```
┌─────────────────────────────────────────────────────────────────────┐
│                         AppDbContext                                 │
│                    (DbContext : EF Core)                             │
├─────────────────────────────────────────────────────────────────────┤
│ + Admins: DbSet<Admin>                                              │
│ + Categories: DbSet<Category>                                        │
│ + Articles: DbSet<Article>                                          │
│ + Tags: DbSet<Tag>                                                  │
│ + ArticleTags: DbSet<ArticleTag>                                    │
│ + Comments: DbSet<Comment>                                          │
│ + AILogs: DbSet<AILog>                                              │
│ + NewsletterSubscribers: DbSet<NewsletterSubscriber>                │
│ + CommentVotes: DbSet<CommentVote>                                  │
│ + ViewHistories: DbSet<ViewHistory>                                │
│ + FeedbackReports: DbSet<FeedbackReport>                            │
│ + Users: DbSet<User>                                                │
│ + ChatGroups: DbSet<ChatGroup>                                      │
│ + ChatMembers: DbSet<ChatMember>                                    │
│ + ChatMessages: DbSet<ChatMessage>                                  │
│ + Friendships: DbSet<Friendship>                                    │
│ + UserFollows: DbSet<UserFollow>                                    │
│ + SavedArticles: DbSet<SavedArticle>                               │
│ + UserCategoryFollows: DbSet<UserCategoryFollow>                   │
│ + Videos: DbSet<Video>                                              │
│ + RssSources: DbSet<RssSource>                                      │
├─────────────────────────────────────────────────────────────────────┤
│ + OnModelCreating(ModelBuilder mb) : void                           │
└─────────────────────────────────────────────────────────────────────┘
```

## SignalR Hub

```
┌─────────────────────────────────────────────────────────────────────┐
│                           ChatHub                                    │
│                         (Hub : SignalR)                              │
├─────────────────────────────────────────────────────────────────────┤
│ - _db: AppDbContext                                                │
├─────────────────────────────────────────────────────────────────────┤
│ + JoinGroup(int groupId) : Task                                     │
│ + SendMessage(int groupId, string content) : Task                    │
│ + OnConnectedAsync() : Task                                         │
│ + OnDisconnectedAsync(Exception? exception) : Task                  │
└─────────────────────────────────────────────────────────────────────┘
```

## Key Relationships Summary

### One-to-Many Relationships
- **Category** → **Article** (1 category has many articles)
- **Category** → **Category** (self-reference for parent-child)
- **Admin** → **Article** (1 admin writes many articles)
- **User** → **Article** (1 user writes many articles)
- **Article** → **Comment** (1 article has many comments)
- **Comment** → **Comment** (self-reference for replies)
- **Article** → **AILog** (1 article has many AI logs)
- **Article** → **ViewHistory** (1 article has many view histories)
- **ChatGroup** → **ChatMember** (1 group has many members)
- **ChatGroup** → **ChatMessage** (1 group has many messages)
- **ChatMessage** → **ChatMessage** (self-reference for replies)

### Many-to-Many Relationships
- **Article** ↔ **Tag** (via ArticleTag junction table)
- **User** ↔ **User** (via Friendship - friend requests)
- **User** ↔ **User** (via UserFollow - following)

### One-to-One Relationships (via unique indexes)
- **User** ↔ **SavedArticle** (user saves article)
- **User** ↔ **UserCategoryFollow** (user follows category)
- **Comment** ↔ **CommentVote** (session-based voting)

### Dependencies
- All **Controllers** depend on **AppDbContext**
- **AdminController** depends on: ImageUploadService, AIService, EmailService, VideoUploadService
- **ArticleController** depends on: AIService
- **RssController** depends on: NewsImportService
- **ChatHub** depends on: AppDbContext
