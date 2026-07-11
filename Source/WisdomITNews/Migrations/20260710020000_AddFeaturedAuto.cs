using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(WisdomITNews.Data.AppDbContext))]
    [Migration("20260710020000_AddFeaturedAuto")]
    public class AddFeaturedAuto : Migration
    {
        /// <inheritdoc />
        // SQL có kiểm tra tồn tại (IF NOT EXISTS) để an toàn dù DB đang ở trạng thái nào.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'FeaturedPinned' AND Object_ID = OBJECT_ID(N'[Articles]'))
BEGIN
    ALTER TABLE [Articles] ADD [FeaturedPinned] bit NOT NULL CONSTRAINT [DF_Articles_FeaturedPinned] DEFAULT (0);
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'FeaturedHidden' AND Object_ID = OBJECT_ID(N'[Articles]'))
BEGIN
    ALTER TABLE [Articles] ADD [FeaturedHidden] bit NOT NULL CONSTRAINT [DF_Articles_FeaturedHidden] DEFAULT (0);
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Articles_FeaturedPinned' AND object_id = OBJECT_ID(N'[Articles]'))
BEGIN
    CREATE INDEX [IX_Articles_FeaturedPinned] ON [Articles] ([FeaturedPinned]);
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Articles_FeaturedHidden' AND object_id = OBJECT_ID(N'[Articles]'))
BEGIN
    CREATE INDEX [IX_Articles_FeaturedHidden] ON [Articles] ([FeaturedHidden]);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'FeaturedPinned' AND Object_ID = OBJECT_ID(N'[Articles]')) ALTER TABLE [Articles] DROP COLUMN [FeaturedPinned];");
            migrationBuilder.Sql(@"IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'FeaturedHidden' AND Object_ID = OBJECT_ID(N'[Articles]')) ALTER TABLE [Articles] DROP COLUMN [FeaturedHidden];");
        }
    }
}
