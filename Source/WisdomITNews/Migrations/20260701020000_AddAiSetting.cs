using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class AddAiSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApiVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Temperature = table.Column<double>(type: "float", nullable: false),
                    MaxOutputTokens = table.Column<int>(type: "int", nullable: false),
                    ThinkingBudget = table.Column<int>(type: "int", nullable: false),
                    SystemInstruction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SummarizeLength = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SummarizeTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SuggestTitleTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModerateTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChatMaxSentences = table.Column<int>(type: "int", nullable: false),
                    ChatTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiSettings");
        }
    }
}
