using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminStaffFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Gender", table: "Admins", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "Address", table: "Admins", type: "nvarchar(max)", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Gender", table: "Admins");
            migrationBuilder.DropColumn(name: "Address", table: "Admins");
        }
    }
}
