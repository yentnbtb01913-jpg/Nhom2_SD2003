using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoUploadFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "Videos",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoType",
                table: "Videos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoUrl",
                table: "Videos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "Videos",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("UPDATE Videos SET VideoType = 'youtube' WHERE VideoType IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "FileSize", table: "Videos");
            migrationBuilder.DropColumn(name: "VideoType", table: "Videos");
            migrationBuilder.DropColumn(name: "VideoUrl", table: "Videos");
            migrationBuilder.DropColumn(name: "CreatedByUserId", table: "Videos");
        }
    }
}
