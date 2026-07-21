using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class AdLayoutOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AdSlots",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "Advertisements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AdZoneSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Position = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RotationSeconds = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdZoneSettings", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "AdSlots",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "Name", "SlotKey" },
                values: new object[] { "Banner ngang 728x90 nằm trên cùng, phía trên cả logo — mọi trang", "Banner Đầu Trang (ngang)", "header" });

            migrationBuilder.UpdateData(
                table: "AdSlots",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "Name", "Size", "SlotKey" },
                values: new object[] { "Dải dọc 160x600 bên trái trang chủ", "Dải Dọc Trái", "Skyscraper160x600", "home_left" });

            migrationBuilder.UpdateData(
                table: "AdSlots",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "Name", "PricePerDay", "Size", "SlotKey" },
                values: new object[] { "Dải dọc 160x600 bên phải trang chủ", "Dải Dọc Phải", 300000m, "Skyscraper160x600", "home_right" });

            migrationBuilder.UpdateData(
                table: "AdSlots",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "SlotKey" },
                values: new object[] { "Banner 728x90 chèn giữa nội dung bài viết", "in_article" });

            migrationBuilder.UpdateData(
                table: "AdSlots",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Description", "Name", "Size", "SlotKey" },
                values: new object[] { "Quảng cáo 300x250 ở sidebar cạnh bài viết", "Cạnh Bài Viết", "Rectangle300x250", "sidebar" });

            migrationBuilder.InsertData(
                table: "AdZoneSettings",
                columns: new[] { "Id", "Position", "RotationSeconds" },
                values: new object[,]
                {
                    { 1, "header", 5 },
                    { 2, "home_left", 5 },
                    { 3, "home_right", 5 },
                    { 4, "in_article", 5 },
                    { 5, "sidebar", 5 }
                });

            migrationBuilder.UpdateData(
                table: "Admins",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$92Oo/B.K/yM4exSZqzZHeu7PIVTAytaH5vnt78RFNDwbIkyFy0gE2");

            migrationBuilder.CreateIndex(
                name: "IX_AdZoneSettings_Position",
                table: "AdZoneSettings",
                column: "Position",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdZoneSettings");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "Advertisements");

            migrationBuilder.UpdateData(
                table: "AdSlots",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Description", "Name", "SlotKey" },
                values: new object[] { "Banner 728x90 hiển thị ở đầu mỗi trang bài viết", "Banner Đầu Trang", "TopBanner" });

            migrationBuilder.UpdateData(
                table: "AdSlots",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "Description", "Name", "Size", "SlotKey" },
                values: new object[] { "Quảng cáo 300x250 ở sidebar bên phải, vị trí trên", "Sidebar Trên", "Rectangle300x250", "SidebarTop" });

            migrationBuilder.UpdateData(
                table: "AdSlots",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Description", "Name", "PricePerDay", "Size", "SlotKey" },
                values: new object[] { "Quảng cáo 300x250 ở sidebar bên phải, vị trí dưới", "Sidebar Dưới", 200000m, "Rectangle300x250", "SidebarBottom" });

            migrationBuilder.UpdateData(
                table: "AdSlots",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "SlotKey" },
                values: new object[] { "Quảng cáo 728x90 chèn giữa nội dung bài viết", "ArticleInline" });

            migrationBuilder.UpdateData(
                table: "AdSlots",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "Description", "Name", "Size", "SlotKey" },
                values: new object[] { "Quảng cáo 728x90 hiển thị cuối bài viết", "Cuối Bài Viết", "Banner728x90", "ArticleBottom" });

            migrationBuilder.InsertData(
                table: "AdSlots",
                columns: new[] { "Id", "CreatedAt", "Description", "IsActive", "Name", "PricePerDay", "Size", "SlotKey" },
                values: new object[] { 6, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Banner 728x90 ở footer website", true, "Footer Banner", 150000m, "Banner728x90", "FooterBanner" });

            migrationBuilder.UpdateData(
                table: "Admins",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$hLJx12Lqjc1kJuyFL1YSwOyjFix7u6oMg5LKgmYaXQXHJ6aLRwu16");
        }
    }
}
