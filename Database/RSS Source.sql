Use WisdomITNews	
go
    
    CREATE TABLE [RssSources] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [Name] NVARCHAR(MAX) NOT NULL,
    [FeedUrl] NVARCHAR(MAX) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,
    [WebsiteUrl] NVARCHAR(MAX) NULL,
    [Country] NVARCHAR(MAX) NULL,
    [IsActive] BIT NOT NULL DEFAULT 1,
    [DefaultCategoryId] INT NULL,
    [MaxImport] INT NOT NULL DEFAULT 30,
    [LastImportAt] DATETIME2 NULL,
    [TotalImported] INT NOT NULL DEFAULT 0,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE()
);

INSERT INTO [RssSources] ([Name], [FeedUrl], [WebsiteUrl], [Country], [Description], [IsActive], [DefaultCategoryId], [MaxImport], [TotalImported], [CreatedAt])
VALUES 
('The Hacker News', 'https://feeds.feedburner.com/TheHackersNews', 'https://thehackernews.com', 'US', 'Tin bảo mật & công nghệ quốc tế', 1, 7, 30, 0, GETDATE()),
('Cục An toàn Thông tin', 'https://khonggian.gov.vn/rss', 'https://khonggian.gov.vn', 'VN', 'Tin chính thức từ Cục ATTT Việt Nam', 1, 7, 20, 0, GETDATE()),
('VietNamNet IT', 'https://vietnamnet.vn/cong-nghe.rss', 'https://vietnamnet.vn/cong-nghe', 'VN', 'Tin công nghệ trong nước cập nhật hàng ngày', 1, 1, 30, 0, GETDATE()),
('Tạp chí An toàn Thông tin', 'https://antoanthongtin.vn/rss', 'https://antoanthongtin.vn', 'VN', 'Tin chuyên sâu về bảo mật và an toàn thông tin', 1, 7, 20, 0, GETDATE());

UPDATE [RssSources] SET [Name] = N'Cục An toàn Thông tin' WHERE [Name] LIKE 'C?c%';
UPDATE [RssSources] SET [Name] = N'Tạp chí An toàn Thông tin' WHERE [Name] LIKE 'T?p%';


-- 1. The Hacker News → giữ nguyên URL
UPDATE [RssSources] 
SET [FeedUrl] = N'https://feeds.feedburner.com/TheHackersNews',
    [WebsiteUrl] = N'https://thehackernews.com',
    [Country] = N'US',
    [Description] = N'Tin bảo mật & công nghệ quốc tế'
WHERE [Name] LIKE N'%Hacker%';

-- 2. VietNamNet Công nghệ → cập nhật URL đúng
UPDATE [RssSources] 
SET [Name] = N'VietNamNet Công nghệ',
    [FeedUrl] = N'https://vietnamnet.vn/rss/cong-nghe.rss',
    [WebsiteUrl] = N'https://vietnamnet.vn/cong-nghe',
    [Country] = N'VN',
    [Description] = N'Tin công nghệ trong nước cập nhật hàng ngày'
WHERE [Name] LIKE N'%VietNamNet%';

-- 3. Thay Cục ATTT → Thanh Niên Công nghệ
UPDATE [RssSources]
SET [Name] = N'Thanh Niên Công nghệ',
    [FeedUrl] = N'https://thanhnien.vn/rss/cong-nghe.rss',
    [WebsiteUrl] = N'https://thanhnien.vn/cong-nghe',
    [Country] = N'VN',
    [Description] = N'Tin công nghệ từ báo Thanh Niên'
WHERE [Name] LIKE N'%C_c%' 
   OR [Name] LIKE N'%ATTT%' 
   OR [Name] LIKE N'%ICT%'
   OR [Name] LIKE N'%An to%';

-- 4. Thay Tạp chí ATTT → GenK Công nghệ
UPDATE [RssSources]
SET [Name] = N'GenK Công nghệ',
    [FeedUrl] = N'https://genk.vn/index.rss',
    [WebsiteUrl] = N'https://genk.vn',
    [Country] = N'VN',
    [Description] = N'Tin công nghệ, gadget, internet Việt Nam'
WHERE [Name] LIKE N'%T_p ch%' 
   OR [Name] LIKE N'%Th_ng tin%'
   OR [Name] LIKE N'%VnExpress%';

-- Kiểm tra kết quả
SELECT [Id], [Name], [FeedUrl], [Country], [IsActive] 
FROM [RssSources] 
ORDER BY [Id];