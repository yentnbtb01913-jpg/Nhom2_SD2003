using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class AddAiCategoryMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImportedBy",
                table: "Articles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CategoryMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AiLabel = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryMappings_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiCategoryCorrectionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(type: "int", nullable: true),
                    EditorName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OldCategory = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NewCategory = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiCategoryCorrectionLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryMappings_AiLabel",
                table: "CategoryMappings",
                column: "AiLabel");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryMappings_CategoryId",
                table: "CategoryMappings",
                column: "CategoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AiCategoryCorrectionLogs");
            migrationBuilder.DropTable(name: "CategoryMappings");
            migrationBuilder.DropColumn(name: "ImportedBy", table: "Articles");
        }
    }
}
