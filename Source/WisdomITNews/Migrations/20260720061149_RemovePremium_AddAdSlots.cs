using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class RemovePremium_AddAdSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SlotKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Size = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PricePerDay = table.Column<decimal>(type: "decimal(18,0)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdSlots", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AdSlots",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name", "PricePerDay", "Size", "SlotKey" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Banner 728x90 hiển thị ở đầu mỗi trang bài viết", true, "Banner Đầu Trang", 500000m, "Banner728x90", "TopBanner" },
                    { 2, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Quảng cáo 300x250 ở sidebar bên phải, vị trí trên", true, "Sidebar Trên", 300000m, "Rectangle300x250", "SidebarTop" },
                    { 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Quảng cáo 300x250 ở sidebar bên phải, vị trí dưới", true, "Sidebar Dưới", 200000m, "Rectangle300x250", "SidebarBottom" },
                    { 4, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Quảng cáo 728x90 chèn giữa nội dung bài viết", true, "Giữa Bài Viết", 400000m, "Banner728x90", "ArticleInline" },
                    { 5, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Quảng cáo 728x90 hiển thị cuối bài viết", true, "Cuối Bài Viết", 250000m, "Banner728x90", "ArticleBottom" },
                    { 6, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Banner 728x90 ở footer website", true, "Footer Banner", 150000m, "Banner728x90", "FooterBanner" }
                });

            migrationBuilder.UpdateData(
                table: "Admins",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$mzCNEu6VpXfYM9Hlor4e0OzbrpfMiawzYRcmqAe9P6EmFkwofc5oi");

            migrationBuilder.CreateIndex(
                name: "IX_AdSlots_SlotKey",
                table: "AdSlots",
                column: "SlotKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdSlots");

            migrationBuilder.UpdateData(
                table: "Admins",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$uqDn1FgAiAg3oogdp5hv3u1qXUNU3/uAXKMVp9xO1av4jpFemirDG");
        }
    }
}
