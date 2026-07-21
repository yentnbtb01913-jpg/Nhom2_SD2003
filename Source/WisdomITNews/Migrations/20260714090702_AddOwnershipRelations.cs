using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnershipRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UpdatedByAdminId",
                table: "AutoImportSettings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByAdminId",
                table: "AiSettings",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Admins",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$6/m28Z4dH8RIWgSBz/jr0.Ww49zwCx6ZNVm.6HYGseogp.JitTyH.");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_CreatedByAdminId",
                table: "Videos",
                column: "CreatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_CreatedByUserId",
                table: "Videos",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_SentByAdminId",
                table: "Notifications",
                column: "SentByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoImportSettings_UpdatedByAdminId",
                table: "AutoImportSettings",
                column: "UpdatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_AiSettings_UpdatedByAdminId",
                table: "AiSettings",
                column: "UpdatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_AiCategoryCorrectionLogs_ArticleId",
                table: "AiCategoryCorrectionLogs",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_Advertisements_CreatedByAdminId",
                table: "Advertisements",
                column: "CreatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Advertisements_CreatedByUserId",
                table: "Advertisements",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Advertisements_Admins_CreatedByAdminId",
                table: "Advertisements",
                column: "CreatedByAdminId",
                principalTable: "Admins",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Advertisements_Users_CreatedByUserId",
                table: "Advertisements",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AiCategoryCorrectionLogs_Articles_ArticleId",
                table: "AiCategoryCorrectionLogs",
                column: "ArticleId",
                principalTable: "Articles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AiSettings_Admins_UpdatedByAdminId",
                table: "AiSettings",
                column: "UpdatedByAdminId",
                principalTable: "Admins",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AutoImportSettings_Admins_UpdatedByAdminId",
                table: "AutoImportSettings",
                column: "UpdatedByAdminId",
                principalTable: "Admins",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Admins_SentByAdminId",
                table: "Notifications",
                column: "SentByAdminId",
                principalTable: "Admins",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Videos_Admins_CreatedByAdminId",
                table: "Videos",
                column: "CreatedByAdminId",
                principalTable: "Admins",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Videos_Users_CreatedByUserId",
                table: "Videos",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Advertisements_Admins_CreatedByAdminId",
                table: "Advertisements");

            migrationBuilder.DropForeignKey(
                name: "FK_Advertisements_Users_CreatedByUserId",
                table: "Advertisements");

            migrationBuilder.DropForeignKey(
                name: "FK_AiCategoryCorrectionLogs_Articles_ArticleId",
                table: "AiCategoryCorrectionLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AiSettings_Admins_UpdatedByAdminId",
                table: "AiSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_AutoImportSettings_Admins_UpdatedByAdminId",
                table: "AutoImportSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Admins_SentByAdminId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Videos_Admins_CreatedByAdminId",
                table: "Videos");

            migrationBuilder.DropForeignKey(
                name: "FK_Videos_Users_CreatedByUserId",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "IX_Videos_CreatedByAdminId",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "IX_Videos_CreatedByUserId",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_SentByAdminId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_AutoImportSettings_UpdatedByAdminId",
                table: "AutoImportSettings");

            migrationBuilder.DropIndex(
                name: "IX_AiSettings_UpdatedByAdminId",
                table: "AiSettings");

            migrationBuilder.DropIndex(
                name: "IX_AiCategoryCorrectionLogs_ArticleId",
                table: "AiCategoryCorrectionLogs");

            migrationBuilder.DropIndex(
                name: "IX_Advertisements_CreatedByAdminId",
                table: "Advertisements");

            migrationBuilder.DropIndex(
                name: "IX_Advertisements_CreatedByUserId",
                table: "Advertisements");

            migrationBuilder.DropColumn(
                name: "UpdatedByAdminId",
                table: "AutoImportSettings");

            migrationBuilder.DropColumn(
                name: "UpdatedByAdminId",
                table: "AiSettings");

            migrationBuilder.UpdateData(
                table: "Admins",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$ujkrQxfTraVQSJoe7iNLuOY3G1T.sCZP7DZRxOQNdz8IN9BPQN1/.");
        }
    }
}
