using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InterestedCategory",
                table: "NewsletterSubscribers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NewsletterEmailLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubscriberId = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Segment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterEmailLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterEmailLogs_SubscriberId",
                table: "NewsletterEmailLogs",
                column: "SubscriberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "NewsletterEmailLogs");
            migrationBuilder.DropColumn(name: "InterestedCategory", table: "NewsletterSubscribers");
        }
    }
}
