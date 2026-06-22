-- =====================================================
-- WisdomITNews - SQL Server Database Schema (ERD)
-- Generated from ASP.NET Core Entity Framework Models
-- Database: SQL Server (MSSQL)
-- =====================================================
DROP DATABASE IF EXISTS WisdomITNewsDb;
GO
CREATE DATABASE WisdomITNewsDb;
GO
USE WisdomITNewsDb;
GO

-- =====================================================
-- 1. BẢNG ADMINS (Quản trị viên)
-- =====================================================
CREATE TABLE [dbo].[Admins] (
    [Id]            INT             IDENTITY(1,1) NOT NULL,
    [Username]      NVARCHAR(100)   NOT NULL,
    [PasswordHash]  NVARCHAR(500)   NOT NULL,
    [FullName]      NVARCHAR(200)   NOT NULL,
    [Email]         NVARCHAR(200)   NOT NULL,
    [Role]          NVARCHAR(50)    NOT NULL DEFAULT N'editor',
    [IsActive]      BIT             NOT NULL DEFAULT 1,
    [LastLogin]     DATETIME2       NULL,
    [CreatedAt]     DATETIME2       NOT NULL DEFAULT GETDATE(),

    CONSTRAINT [PK_Admins] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Admins_Username] UNIQUE ([Username])
);
GO

-- =====================================================
-- 2. BẢNG USERS (Người dùng)
-- =====================================================
CREATE TABLE [dbo].[Users] (
    [Id]                INT             IDENTITY(1,1) NOT NULL,
    [Username]          NVARCHAR(100)   NOT NULL,
    [Email]             NVARCHAR(200)   NOT NULL,
    [PasswordHash]      NVARCHAR(500)   NOT NULL,
    [FullName]          NVARCHAR(200)   NOT NULL,
    [AvatarUrl]         NVARCHAR(500)   NULL,
    [Bio]               NVARCHAR(MAX)   NULL,
    [Role]              NVARCHAR(50)    NOT NULL DEFAULT N'Reader',
    [IsActive]          BIT             NOT NULL DEFAULT 1,
    [IsEmailConfirmed]  BIT             NOT NULL DEFAULT 0,
    [CreatedAt]         DATETIME2       NOT NULL DEFAULT GETDATE(),

    CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Users_Username] UNIQUE ([Username]),
    CONSTRAINT [UQ_Users_Email] UNIQUE ([Email])
);
GO

-- =====================================================
-- 3. BẢNG CATEGORIES (Danh mục - tự tham chiếu)
-- =====================================================
CREATE TABLE [dbo].[Categories] (
    [Id]                INT             IDENTITY(1,1) NOT NULL,
    [Name]              NVARCHAR(200)   NOT NULL,
    [Slug]              NVARCHAR(200)   NOT NULL,
    [Description]       NVARCHAR(MAX)   NULL,
    [Icon]              NVARCHAR(50)    NULL,
    [Color]             NVARCHAR(20)    NOT NULL DEFAULT N'#e63946',
    [SortOrder]         INT             NOT NULL DEFAULT 0,
    [IsVisible]         BIT             NOT NULL DEFAULT 1,
    [ParentCategoryId]  INT             NULL,
    [CreatedAt]         DATETIME2       NOT NULL DEFAULT GETDATE(),
    [UpdatedAt]         DATETIME2       NOT NULL DEFAULT GETDATE(),

    CONSTRAINT [PK_Categories] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Categories_Slug] UNIQUE ([Slug]),
    CONSTRAINT [FK_Categories_ParentCategory] FOREIGN KEY ([ParentCategoryId])
        REFERENCES [dbo].[Categories]([Id]) ON DELETE NO ACTION
);
GO

-- =====================================================
-- 4. BẢNG ARTICLES (Bài viết)
-- =====================================================
CREATE TABLE [dbo].[Articles] (
    [Id]            INT             IDENTITY(1,1) NOT NULL,
    [Title]         NVARCHAR(500)   NOT NULL,
    [Slug]          NVARCHAR(500)   NOT NULL,
    [Summary]       NVARCHAR(1000)  NOT NULL,
    [Content]       NVARCHAR(MAX)   NOT NULL,
    [Thumbnail]     NVARCHAR(500)   NULL,
    [ThumbnailAlt]  NVARCHAR(300)   NULL,
    [CategoryId]    INT             NULL,
    [AuthorId]      INT             NULL,
    [Views]         INT             NOT NULL DEFAULT 0,
    [Status]        NVARCHAR(20)    NOT NULL DEFAULT N'draft',
    [IsFeatured]    BIT             NOT NULL DEFAULT 0,
    [IsBreaking]    BIT             NOT NULL DEFAULT 0,
    [PublishedAt]   DATETIME2       NULL,
    [AiSummary]     NVARCHAR(MAX)   NULL,
    [MetaTitle]     NVARCHAR(300)   NULL,
    [MetaDesc]      NVARCHAR(500)   NULL,
    [Region]        NVARCHAR(100)   NULL,
    [Latitude]      FLOAT           NULL,
    [Longitude]     FLOAT           NULL,
    [AuthorUserId]  INT             NULL,
    [CreatedAt]     DATETIME2       NOT NULL DEFAULT GETDATE(),
    [UpdatedAt]     DATETIME2       NOT NULL DEFAULT GETDATE(),

    CONSTRAINT [PK_Articles] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Articles_Slug] UNIQUE ([Slug]),
    CONSTRAINT [FK_Articles_Category] FOREIGN KEY ([CategoryId])
        REFERENCES [dbo].[Categories]([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_Articles_Author] FOREIGN KEY ([AuthorId])
        REFERENCES [dbo].[Admins]([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_Articles_AuthorUser] FOREIGN KEY ([AuthorUserId])
        REFERENCES [dbo].[Users]([Id]) ON DELETE SET NULL
);
GO

CREATE INDEX [IX_Articles_Status] ON [dbo].[Articles]([Status]);
CREATE INDEX [IX_Articles_Region] ON [dbo].[Articles]([Region]);
GO

-- =====================================================
-- 5. BẢNG TAGS (Thẻ tag)
-- =====================================================
CREATE TABLE [dbo].[Tags] (
    [Id]        INT             IDENTITY(1,1) NOT NULL,
    [Name]      NVARCHAR(100)   NOT NULL,
    [Slug]      NVARCHAR(100)   NOT NULL,
    [CreatedAt] DATETIME2       NOT NULL DEFAULT GETDATE(),

    CONSTRAINT [PK_Tags] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_Tags_Slug] UNIQUE ([Slug])
);
GO

-- =====================================================
-- 6. BẢNG ARTICLETAGS (Bảng trung gian N-N)
-- =====================================================
CREATE TABLE [dbo].[ArticleTags] (
    [ArticleId] INT NOT NULL,
    [TagId]     INT NOT NULL,

    CONSTRAINT [PK_ArticleTags] PRIMARY KEY CLUSTERED ([ArticleId], [TagId]),
    CONSTRAINT [FK_ArticleTags_Article] FOREIGN KEY ([ArticleId])
        REFERENCES [dbo].[Articles]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ArticleTags_Tag] FOREIGN KEY ([TagId])
        REFERENCES [dbo].[Tags]([Id]) ON DELETE CASCADE
);
GO

-- =====================================================
-- 7. BẢNG COMMENTS (Bình luận - tự tham chiếu)
-- =====================================================
CREATE TABLE [dbo].[Comments] (
    [Id]                INT             IDENTITY(1,1) NOT NULL,
    [ArticleId]         INT             NOT NULL,
    [AuthorName]        NVARCHAR(200)   NOT NULL,
    [AuthorEmail]       NVARCHAR(200)   NULL,
    [Content]           NVARCHAR(MAX)   NOT NULL,
    [ParentId]          INT             NULL,
    [Likes]             INT             NOT NULL DEFAULT 0,
    [Status]            NVARCHAR(20)    NOT NULL DEFAULT N'pending',
    [CreatedAt]         DATETIME2       NOT NULL DEFAULT GETDATE(),
    [ParentCommentId]   INT             NULL,
    [LikeCount]         INT             NOT NULL DEFAULT 0,
    [DislikeCount]      INT             NOT NULL DEFAULT 0,
    [UserId]            INT             NULL,

    CONSTRAINT [PK_Comments] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Comments_Article] FOREIGN KEY ([ArticleId])
        REFERENCES [dbo].[Articles]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Comments_ParentComment] FOREIGN KEY ([ParentCommentId])
        REFERENCES [dbo].[Comments]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Comments_User] FOREIGN KEY ([UserId])
        REFERENCES [dbo].[Users]([Id]) ON DELETE SET NULL
);
GO

-- =====================================================
-- 8. BẢNG COMMENTVOTES (Vote bình luận)
-- =====================================================
CREATE TABLE [dbo].[CommentVotes] (
    [Id]        INT             IDENTITY(1,1) NOT NULL,
    [CommentId] INT             NOT NULL,
    [SessionId] NVARCHAR(200)   NOT NULL,
    [VoteType]  NVARCHAR(20)    NOT NULL DEFAULT N'Like',
    [CreatedAt] DATETIME2       NOT NULL DEFAULT GETDATE(),

    CONSTRAINT [PK_CommentVotes] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_CommentVotes_Comment] FOREIGN KEY ([CommentId])
        REFERENCES [dbo].[Comments]([Id]) ON DELETE CASCADE,
    CONSTRAINT [UQ_CommentVotes_CommentSession] UNIQUE ([CommentId], [SessionId])
);
GO

-- =====================================================
-- 9. BẢNG AILOGS (Lịch sử gọi AI)
-- =====================================================
CREATE TABLE [dbo].[AILogs] (
    [Id]            BIGINT          IDENTITY(1,1) NOT NULL,
    [ArticleId]     INT             NULL,
    [Action]        NVARCHAR(100)   NOT NULL DEFAULT N'',
    [PromptText]    NVARCHAR(MAX)   NULL,
    [ResultText]    NVARCHAR(MAX)   NULL,
    [ModelUsed]     NVARCHAR(100)   NULL,
    [TokensUsed]    INT             NOT NULL DEFAULT 0,
    [IsSuccess]     BIT             NOT NULL DEFAULT 1,
    [ErrorMsg]      NVARCHAR(MAX)   NULL,
    [CreatedAt]     DATETIME2       NOT NULL DEFAULT GETDATE(),

    CONSTRAINT [PK_AILogs] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_AILogs_Article] FOREIGN KEY ([ArticleId])
        REFERENCES [dbo].[Articles]([Id]) ON DELETE SET NULL
);
GO

-- =====================================================
-- 10. BẢNG VIEWHISTORIES (Lịch sử xem bài)
-- =====================================================
CREATE TABLE [dbo].[ViewHistories] (
    [Id]        INT             IDENTITY(1,1) NOT NULL,
    [ArticleId] INT             NOT NULL,
    [SessionId] NVARCHAR(200)   NOT NULL,
    [ViewedAt]  DATETIME2       NOT NULL DEFAULT GETDATE(),

    CONSTRAINT [PK_ViewHistories] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_ViewHistories_Article] FOREIGN KEY ([ArticleId])
        REFERENCES [dbo].[Articles]([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_ViewHistories_SessionId] ON [dbo].[ViewHistories]([SessionId]);
CREATE INDEX [IX_ViewHistories_ViewedAt] ON [dbo].[ViewHistories]([ViewedAt]);
GO

-- =====================================================
-- 11. BẢNG NEWSLETTERSUBSCRIBERS (Đăng ký newsletter)
-- =====================================================
CREATE TABLE [dbo].[NewsletterSubscribers] (
    [Id]            INT             IDENTITY(1,1) NOT NULL,
    [Email]         NVARCHAR(200)   NOT NULL,
    [Status]        NVARCHAR(20)    NOT NULL DEFAULT N'active',
    [SubscribedAt]  DATETIME2       NOT NULL DEFAULT GETDATE(),

    CONSTRAINT [PK_NewsletterSubscribers] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ_NewsletterSubscribers_Email] UNIQUE ([Email])
);
GO

-- =====================================================
-- 12. BẢNG FEEDBACKREPORTS (Báo cáo/Góp ý)
-- =====================================================
CREATE TABLE [dbo].[FeedbackReports] (
    [Id]            INT             IDENTITY(1,1) NOT NULL,
    [PageUrl]       NVARCHAR(500)   NULL,
    [Type]          NVARCHAR(50)    NOT NULL DEFAULT N'other',
    [Description]   NVARCHAR(MAX)   NOT NULL,
    [CreatedAt]     DATETIME2       NOT NULL DEFAULT GETDATE(),
    [IsResolved]    BIT             NOT NULL DEFAULT 0,

    CONSTRAINT [PK_FeedbackReports] PRIMARY KEY CLUSTERED ([Id])
);
GO

CREATE INDEX [IX_FeedbackReports_IsResolved] ON [dbo].[FeedbackReports]([IsResolved]);
CREATE INDEX [IX_FeedbackReports_CreatedAt] ON [dbo].[FeedbackReports]([CreatedAt]);
GO

-- =====================================================
-- 13. BẢNG CHATGROUPS (Nhóm chat)
-- =====================================================
CREATE TABLE [dbo].[ChatGroups] (
    [Id]                INT             IDENTITY(1,1) NOT NULL,
    [Name]              NVARCHAR(200)   NOT NULL,
    [Avatar]            NVARCHAR(500)   NULL,
    [CreatorType]       NVARCHAR(20)    NOT NULL DEFAULT N'user',
    [CreatorId]         INT             NOT NULL,
    [IsDirectMessage]   BIT             NOT NULL DEFAULT 0,
    [CreatedAt]         DATETIME2       NOT NULL DEFAULT GETDATE(),

    CONSTRAINT [PK_ChatGroups] PRIMARY KEY CLUSTERED ([Id])
);
GO

-- =====================================================
-- 14. BẢNG CHATMEMBERS (Thành viên nhóm chat)
-- =====================================================
CREATE TABLE [dbo].[ChatMembers] (
    [Id]            INT             IDENTITY(1,1) NOT NULL,
    [GroupId]       INT             NOT NULL,
    [MemberType]    NVARCHAR(20)    NOT NULL DEFAULT N'user',
    [MemberId]      INT             NOT NULL,
    [Role]          NVARCHAR(20)    NOT NULL DEFAULT N'member',
    [JoinedAt]      DATETIME2       NOT NULL DEFAULT GETDATE(),

    CONSTRAINT [PK_ChatMembers] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_ChatMembers_Group] FOREIGN KEY ([GroupId])
        REFERENCES [dbo].[ChatGroups]([Id]) ON DELETE CASCADE,
    CONSTRAINT [UQ_ChatMembers_GroupMember] UNIQUE ([GroupId], [MemberType], [MemberId])
);
GO

-- =====================================================
-- 15. BẢNG CHATMESSAGES (Tin nhắn chat)
-- =====================================================
CREATE TABLE [dbo].[ChatMessages] (
    [Id]            INT             IDENTITY(1,1) NOT NULL,
    [GroupId]       INT             NOT NULL,
    [SenderType]    NVARCHAR(20)    NOT NULL DEFAULT N'user',
    [SenderId]      INT             NOT NULL,
    [SenderName]    NVARCHAR(200)   NOT NULL DEFAULT N'',
    [SenderAvatar]  NVARCHAR(500)   NULL,
    [Content]       NVARCHAR(MAX)   NOT NULL,
    [MessageType]   NVARCHAR(20)    NOT NULL DEFAULT N'text',
    [FileUrl]       NVARCHAR(500)   NULL,
    [FileName]      NVARCHAR(300)   NULL,
    [ReplyToId]     INT             NULL,
    [IsPinned]      BIT             NOT NULL DEFAULT 0,
    [SentAt]        DATETIME2       NOT NULL DEFAULT GETDATE(),

    CONSTRAINT [PK_ChatMessages] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_ChatMessages_Group] FOREIGN KEY ([GroupId])
        REFERENCES [dbo].[ChatGroups]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ChatMessages_ReplyTo] FOREIGN KEY ([ReplyToId])
        REFERENCES [dbo].[ChatMessages]([Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_ChatMessages_GroupId] ON [dbo].[ChatMessages]([GroupId]);
CREATE INDEX [IX_ChatMessages_SentAt] ON [dbo].[ChatMessages]([SentAt]);
GO

-- =====================================================
-- 16. BẢNG FRIENDSHIPS (Kết bạn)
-- =====================================================
CREATE TABLE [dbo].[Friendships] (
    [Id]            INT             IDENTITY(1,1) NOT NULL,
    [RequesterId]   INT             NOT NULL,
    [ReceiverId]    INT             NOT NULL,
    [Status]        NVARCHAR(20)    NOT NULL DEFAULT N'pending',
    [CreatedAt]     DATETIME2       NOT NULL DEFAULT GETDATE(),
    [AcceptedAt]    DATETIME2       NULL,

    CONSTRAINT [PK_Friendships] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_Friendships_Requester] FOREIGN KEY ([RequesterId])
        REFERENCES [dbo].[Users]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Friendships_Receiver] FOREIGN KEY ([ReceiverId])
        REFERENCES [dbo].[Users]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [UQ_Friendships_RequesterReceiver] UNIQUE ([RequesterId], [ReceiverId])
);
GO

CREATE INDEX [IX_Friendships_Status] ON [dbo].[Friendships]([Status]);
GO

-- =====================================================
-- 17. BẢNG USERFOLLOWS (Theo dõi)
-- =====================================================
CREATE TABLE [dbo].[UserFollows] (
    [Id]            INT             IDENTITY(1,1) NOT NULL,
    [FollowerId]    INT             NOT NULL,
    [FollowingId]   INT             NOT NULL,
    [CreatedAt]     DATETIME2       NOT NULL DEFAULT GETDATE(),

    CONSTRAINT [PK_UserFollows] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [FK_UserFollows_Follower] FOREIGN KEY ([FollowerId])
        REFERENCES [dbo].[Users]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UserFollows_Following] FOREIGN KEY ([FollowingId])
        REFERENCES [dbo].[Users]([Id]) ON DELETE NO ACTION,
    CONSTRAINT [UQ_UserFollows_FollowerFollowing] UNIQUE ([FollowerId], [FollowingId])
);
GO

-- =====================================================
-- SEED DATA: Admin mặc định
-- =====================================================
SET IDENTITY_INSERT [dbo].[Admins] ON;
INSERT INTO [dbo].[Admins] ([Id], [Username], [PasswordHash], [FullName], [Email], [Role], [IsActive], [CreatedAt])
VALUES (1, N'admin', N'$2a$11$xxxHashedPasswordxxx', N'Quản Trị Viên', N'admin@wisdomitnews.vn', N'superadmin', 1, '2025-01-01');
SET IDENTITY_INSERT [dbo].[Admins] OFF;
GO

-- =====================================================
-- SEED DATA: Danh mục mặc định
-- =====================================================
SET IDENTITY_INSERT [dbo].[Categories] ON;
INSERT INTO [dbo].[Categories] ([Id], [Name], [Slug], [Icon], [Color], [SortOrder], [CreatedAt], [UpdatedAt]) VALUES
(1, N'Tin Công Nghệ',         N'tin-cong-nghe',       N'📰', N'#e63946', 1, '2025-01-01', '2025-01-01'),
(2, N'Lập Trình',             N'lap-trinh',           N'💻', N'#2196f3', 2, '2025-01-01', '2025-01-01'),
(3, N'AI & Machine Learning', N'ai-machine-learning', N'🤖', N'#6c63ff', 3, '2025-01-01', '2025-01-01'),
(4, N'Phần Mềm',             N'phan-mem',            N'📱', N'#00897b', 4, '2025-01-01', '2025-01-01'),
(5, N'Phần Cứng',             N'phan-cung',           N'🖥️', N'#f77f00', 5, '2025-01-01', '2025-01-01'),
(6, N'Thủ Thuật IT',          N'thu-thuat-it',        N'💡', N'#ffd166', 6, '2025-01-01', '2025-01-01'),
(7, N'Bảo Mật',               N'bao-mat',             N'🔐', N'#c1121f', 7, '2025-01-01', '2025-01-01'),
(8, N'Điện Toán Đám Mây',     N'dien-toan-dam-may',   N'☁️', N'#0077b6', 8, '2025-01-01', '2025-01-01');
SET IDENTITY_INSERT [dbo].[Categories] OFF;
GO

-- =====================================================
-- TÓM TẮT QUAN HỆ (RELATIONSHIPS)
-- =====================================================
-- 1:N  Admins → Articles (AuthorId)
-- 1:N  Users → Articles (AuthorUserId)
-- 1:N  Categories → Articles (CategoryId)
-- 1:N  Categories → Categories (ParentCategoryId) [Self-ref]
-- N:N  Articles ↔ Tags (qua ArticleTags)
-- 1:N  Articles → Comments (ArticleId)
-- 1:N  Comments → Comments (ParentCommentId) [Self-ref]
-- 1:N  Users → Comments (UserId)
-- 1:N  Comments → CommentVotes (CommentId)
-- 1:N  Articles → AILogs (ArticleId)
-- 1:N  Articles → ViewHistories (ArticleId)
-- 1:N  ChatGroups → ChatMembers (GroupId)
-- 1:N  ChatGroups → ChatMessages (GroupId)
-- 1:N  ChatMessages → ChatMessages (ReplyToId) [Self-ref]
-- 1:N  Users → Friendships (RequesterId)
-- 1:N  Users → Friendships (ReceiverId)
-- 1:N  Users → UserFollows (FollowerId)
-- 1:N  Users → UserFollows (FollowingId)
-- =====================================================
