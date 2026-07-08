using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvertisement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Advertisements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Position = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Impressions = table.Column<int>(type: "int", nullable: false),
                    Clicks = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedByAdminId = table.Column<int>(type: "int", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Advertisements", x => x.Id);
                });


            // Seed 2 quảng cáo demo (header + sidebar) để thấy ngay
            migrationBuilder.InsertData(
                table: "Advertisements",
                columns: new[] { "Title", "ImageUrl", "TargetUrl", "Position", "IsActive", "Status", "Impressions", "Clicks", "CreatedByName", "CreatedAt" },
                values: new object[,]
                {
                    { "Wisdom IT News - Quảng cáo Demo (Header)", "https://placehold.co/728x90/159aa3/white?text=Wisdom+IT+News+-+Quang+Cao", "/", "header", true, "approved", 0, 0, "Hệ thống", new DateTime(2026, 7, 1) },
                    { "Quảng cáo Demo (Sidebar)", "https://placehold.co/300x250/0e7d85/white?text=Quang+Cao+Sidebar", "/", "sidebar", true, "approved", 0, 0, "Hệ thống", new DateTime(2026, 7, 1) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Advertisements");
        }
    }
}
