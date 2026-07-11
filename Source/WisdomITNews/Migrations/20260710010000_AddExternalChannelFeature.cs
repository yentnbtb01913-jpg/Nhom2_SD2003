using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(WisdomITNews.Data.AppDbContext))]
    [Migration("20260710010000_AddExternalChannelFeature")]
    public class AddExternalChannelFeature : Migration
    {
        /// <inheritdoc />
        // Dùng SQL có kiểm tra tồn tại (IF NOT EXISTS) thay vì AddColumn/CreateTable "cứng" của EF,
        // vì DB có thể đã có sẵn cột/bảng này từ một lần Database.Migrate() tự động chạy dở trước đó
        // (auto-migrate lúc khởi động app) mà chưa kịp ghi nhận vào __EFMigrationsHistory.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'LogoUrl' AND Object_ID = OBJECT_ID(N'[RssSources]'))
BEGIN
    ALTER TABLE [RssSources] ADD [LogoUrl] nvarchar(max) NULL;
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE Name = N'SeedViewBatches')
BEGIN
    CREATE TABLE [SeedViewBatches] (
        [Id] int NOT NULL IDENTITY(1,1),
        [Scope] nvarchar(max) NOT NULL,
        [TargetLabel] nvarchar(max) NOT NULL,
        [ArticleCount] int NOT NULL,
        [MinViews] int NOT NULL,
        [MaxViews] int NOT NULL,
        [TotalAdded] bigint NOT NULL,
        [DetailsJson] nvarchar(max) NOT NULL,
        [EditorName] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SeedViewBatches] PRIMARY KEY ([Id])
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SeedViewBatches_CreatedAt' AND object_id = OBJECT_ID(N'[SeedViewBatches]'))
BEGIN
    CREATE INDEX [IX_SeedViewBatches_CreatedAt] ON [SeedViewBatches] ([CreatedAt]);
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SeedViewBatches_Scope' AND object_id = OBJECT_ID(N'[SeedViewBatches]'))
BEGIN
    CREATE INDEX [IX_SeedViewBatches_Scope] ON [SeedViewBatches] ([Scope]);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.tables WHERE Name = N'SeedViewBatches') DROP TABLE [SeedViewBatches];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'LogoUrl' AND Object_ID = OBJECT_ID(N'[RssSources]')) ALTER TABLE [RssSources] DROP COLUMN [LogoUrl];");
        }
    }
}
