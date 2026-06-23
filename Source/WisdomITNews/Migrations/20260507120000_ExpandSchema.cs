using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class ExpandSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ========== Article: thêm cột Region, Latitude, Longitude, AuthorUserId ==========
            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Articles",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Articles",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Articles",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AuthorUserId",
                table: "Articles",
                type: "int",
                nullable: true);

            // ========== Comment: thêm ParentCommentId, LikeCount, DislikeCount, UserId ==========
            migrationBuilder.AddColumn<int>(
                name: "ParentCommentId",
                table: "Comments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LikeCount",
                table: "Comments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DislikeCount",
                table: "Comments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Comments",
                type: "int",
                nullable: true);

            // ========== Category: thêm ParentCategoryId ==========
            migrationBuilder.AddColumn<int>(
                name: "ParentCategoryId",
                table: "Categories",
                type: "int",
                nullable: true);

            // ========== Bảng Users ==========
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AvatarUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            // ========== Bảng CommentVotes ==========
            migrationBuilder.CreateTable(
                name: "CommentVotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommentId = table.Column<int>(type: "int", nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    VoteType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommentVotes_Comments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "Comments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ========== Bảng ViewHistories ==========
            migrationBuilder.CreateTable(
                name: "ViewHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(type: "int", nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ViewedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViewHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ViewHistories_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ========== Bảng FeedbackReports ==========
            migrationBuilder.CreateTable(
                name: "FeedbackReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackReports", x => x.Id);
                });

            // ========== Indexes ==========
            migrationBuilder.CreateIndex(
                name: "IX_Articles_AuthorUserId",
                table: "Articles",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_Region",
                table: "Articles",
                column: "Region");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentCommentId",
                table: "Comments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_UserId",
                table: "Comments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentCategoryId",
                table: "Categories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommentVotes_CommentId_SessionId",
                table: "CommentVotes",
                columns: new[] { "CommentId", "SessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ViewHistories_ArticleId",
                table: "ViewHistories",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_ViewHistories_SessionId",
                table: "ViewHistories",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ViewHistories_ViewedAt",
                table: "ViewHistories",
                column: "ViewedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackReports_IsResolved",
                table: "FeedbackReports",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackReports_CreatedAt",
                table: "FeedbackReports",
                column: "CreatedAt");

            // ========== Foreign Keys ==========
            migrationBuilder.AddForeignKey(
                name: "FK_Articles_Users_AuthorUserId",
                table: "Articles",
                column: "AuthorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Comments_ParentCommentId",
                table: "Comments",
                column: "ParentCommentId",
                principalTable: "Comments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Users_UserId",
                table: "Comments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_ParentCategoryId",
                table: "Categories",
                column: "ParentCategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Categories_Categories_ParentCategoryId", table: "Categories");
            migrationBuilder.DropForeignKey(name: "FK_Comments_Users_UserId", table: "Comments");
            migrationBuilder.DropForeignKey(name: "FK_Comments_Comments_ParentCommentId", table: "Comments");
            migrationBuilder.DropForeignKey(name: "FK_Articles_Users_AuthorUserId", table: "Articles");

            migrationBuilder.DropTable(name: "FeedbackReports");
            migrationBuilder.DropTable(name: "ViewHistories");
            migrationBuilder.DropTable(name: "CommentVotes");
            migrationBuilder.DropTable(name: "Users");

            migrationBuilder.DropIndex(name: "IX_Categories_ParentCategoryId", table: "Categories");
            migrationBuilder.DropIndex(name: "IX_Comments_UserId", table: "Comments");
            migrationBuilder.DropIndex(name: "IX_Comments_ParentCommentId", table: "Comments");
            migrationBuilder.DropIndex(name: "IX_Articles_Region", table: "Articles");
            migrationBuilder.DropIndex(name: "IX_Articles_AuthorUserId", table: "Articles");

            migrationBuilder.DropColumn(name: "ParentCategoryId", table: "Categories");
            migrationBuilder.DropColumn(name: "UserId", table: "Comments");
            migrationBuilder.DropColumn(name: "DislikeCount", table: "Comments");
            migrationBuilder.DropColumn(name: "LikeCount", table: "Comments");
            migrationBuilder.DropColumn(name: "ParentCommentId", table: "Comments");
            migrationBuilder.DropColumn(name: "AuthorUserId", table: "Articles");
            migrationBuilder.DropColumn(name: "Longitude", table: "Articles");
            migrationBuilder.DropColumn(name: "Latitude", table: "Articles");
            migrationBuilder.DropColumn(name: "Region", table: "Articles");
        }
    }
}
