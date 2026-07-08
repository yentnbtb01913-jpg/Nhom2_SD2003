using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamChatMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsGroup = table.Column<bool>(type: "bit", nullable: false),
                    SenderRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SenderId = table.Column<int>(type: "int", nullable: false),
                    SenderName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RecipientRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RecipientId = table.Column<int>(type: "int", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamChatMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamChatMessages_ConversationKey",
                table: "TeamChatMessages",
                column: "ConversationKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TeamChatMessages");
        }
    }
}
