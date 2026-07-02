using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffEmploymentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmploymentStatus",
                table: "Admins",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "working");

            // Giữ lại trạng thái khoá cũ: nhân viên đang bị khoá (IsActive = 0) -> Tạm nghỉ
            migrationBuilder.Sql("UPDATE [Admins] SET [EmploymentStatus] = N'on_leave' WHERE [IsActive] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmploymentStatus",
                table: "Admins");
        }
    }
}
