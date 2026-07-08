using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoImportSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutoImportSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    ScanIntervalSeconds = table.Column<int>(type: "int", nullable: false),
                    MaxPerSource = table.Column<int>(type: "int", nullable: false),
                    DelayBetweenArticlesSeconds = table.Column<int>(type: "int", nullable: false),
                    DelayBetweenSourcesSeconds = table.Column<int>(type: "int", nullable: false),
                    Concurrency = table.Column<int>(type: "int", nullable: false),
                    MaxTotalPerRun = table.Column<int>(type: "int", nullable: false),
                    RetrySeconds = table.Column<int>(type: "int", nullable: false),
                    OnlyNew = table.Column<bool>(type: "bit", nullable: false),
                    LogSuccess = table.Column<bool>(type: "bit", nullable: false),
                    LogSkipDuplicate = table.Column<bool>(type: "bit", nullable: false),
                    LogError = table.Column<bool>(type: "bit", nullable: false),
                    LogConnectionError = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoImportSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AutoImportSettings");
        }
    }
}
