using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCoverUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverUrl",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Admins",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$K2J9XrI02zkIT3P77GWTBu4fiAR6D8e4rsb3UTCADw7FkaumQ40mG");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverUrl",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "Admins",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$bvl1eDw79pAmPBHxn93ZK.qFO1uu3LJaSvhLao534LP3tNPtjv4oa");
        }
    }
}
