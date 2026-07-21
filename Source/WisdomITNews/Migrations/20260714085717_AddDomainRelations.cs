using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class AddDomainRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArticleId",
                table: "Videos",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Scope",
                table: "SeedViewBatches",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Keyword",
                table: "SearchHistories",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "FeedbackReports",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Admins",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$ujkrQxfTraVQSJoe7iNLuOY3G1T.sCZP7DZRxOQNdz8IN9BPQN1/.");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_ArticleId",
                table: "Videos",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCategoryFollows_CategoryId",
                table: "UserCategoryFollows",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffActivityLogs_ActorAdminId",
                table: "StaffActivityLogs",
                column: "ActorAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedArticles_ArticleId",
                table: "SavedArticles",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_RssSources_DefaultCategoryId",
                table: "RssSources",
                column: "DefaultCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RelatedArticleId",
                table: "Notifications",
                column: "RelatedArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TargetUserId",
                table: "Notifications",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackReports_UserId",
                table: "FeedbackReports",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerActivityLogs_Users_UserId",
                table: "CustomerActivityLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FeedbackReports_Users_UserId",
                table: "FeedbackReports",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalistProfiles_Users_UserId",
                table: "JournalistProfiles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NewsletterEmailLogs_NewsletterSubscribers_SubscriberId",
                table: "NewsletterEmailLogs",
                column: "SubscriberId",
                principalTable: "NewsletterSubscribers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Articles_RelatedArticleId",
                table: "Notifications",
                column: "RelatedArticleId",
                principalTable: "Articles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_TargetUserId",
                table: "Notifications",
                column: "TargetUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RssSources_Categories_DefaultCategoryId",
                table: "RssSources",
                column: "DefaultCategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SavedArticles_Articles_ArticleId",
                table: "SavedArticles",
                column: "ArticleId",
                principalTable: "Articles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SavedArticles_Users_UserId",
                table: "SavedArticles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SearchHistories_Users_UserId",
                table: "SearchHistories",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffActivityLogs_Admins_ActorAdminId",
                table: "StaffActivityLogs",
                column: "ActorAdminId",
                principalTable: "Admins",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffActivityLogs_Admins_TargetAdminId",
                table: "StaffActivityLogs",
                column: "TargetAdminId",
                principalTable: "Admins",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StaffProfiles_Admins_AdminId",
                table: "StaffProfiles",
                column: "AdminId",
                principalTable: "Admins",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserCategoryFollows_Categories_CategoryId",
                table: "UserCategoryFollows",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserCategoryFollows_Users_UserId",
                table: "UserCategoryFollows",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Videos_Articles_ArticleId",
                table: "Videos",
                column: "ArticleId",
                principalTable: "Articles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerActivityLogs_Users_UserId",
                table: "CustomerActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_FeedbackReports_Users_UserId",
                table: "FeedbackReports");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalistProfiles_Users_UserId",
                table: "JournalistProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_NewsletterEmailLogs_NewsletterSubscribers_SubscriberId",
                table: "NewsletterEmailLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Articles_RelatedArticleId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_TargetUserId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_RssSources_Categories_DefaultCategoryId",
                table: "RssSources");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedArticles_Articles_ArticleId",
                table: "SavedArticles");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedArticles_Users_UserId",
                table: "SavedArticles");

            migrationBuilder.DropForeignKey(
                name: "FK_SearchHistories_Users_UserId",
                table: "SearchHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffActivityLogs_Admins_ActorAdminId",
                table: "StaffActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffActivityLogs_Admins_TargetAdminId",
                table: "StaffActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffProfiles_Admins_AdminId",
                table: "StaffProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCategoryFollows_Categories_CategoryId",
                table: "UserCategoryFollows");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCategoryFollows_Users_UserId",
                table: "UserCategoryFollows");

            migrationBuilder.DropForeignKey(
                name: "FK_Videos_Articles_ArticleId",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "IX_Videos_ArticleId",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "IX_UserCategoryFollows_CategoryId",
                table: "UserCategoryFollows");

            migrationBuilder.DropIndex(
                name: "IX_StaffActivityLogs_ActorAdminId",
                table: "StaffActivityLogs");

            migrationBuilder.DropIndex(
                name: "IX_SavedArticles_ArticleId",
                table: "SavedArticles");

            migrationBuilder.DropIndex(
                name: "IX_RssSources_DefaultCategoryId",
                table: "RssSources");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_RelatedArticleId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_TargetUserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_FeedbackReports_UserId",
                table: "FeedbackReports");

            migrationBuilder.DropColumn(
                name: "ArticleId",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "FeedbackReports");

            migrationBuilder.AlterColumn<string>(
                name: "Scope",
                table: "SeedViewBatches",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Keyword",
                table: "SearchHistories",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.UpdateData(
                table: "Admins",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$j.lPTihd2ahErWIU93TEe.Eyf04Q46RPNPwYgLXxS4WhnGwPpjwgC");
        }
    }
}
