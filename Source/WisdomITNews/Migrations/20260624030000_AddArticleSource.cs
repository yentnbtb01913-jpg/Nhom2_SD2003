using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsExternal", table: "Articles", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<string>(
                name: "SourceName", table: "Articles", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "SourceUrl", table: "Articles", type: "nvarchar(max)", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsExternal", table: "Articles");
            migrationBuilder.DropColumn(name: "SourceName", table: "Articles");
            migrationBuilder.DropColumn(name: "SourceUrl", table: "Articles");
        }
    }
}
