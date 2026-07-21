using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class BanQuangCao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdSlotId",
                table: "Advertisements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "Advertisements",
                type: "decimal(18,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BuyerPhone",
                table: "Advertisements",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Days",
                table: "Advertisements",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Advertisements",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "Admins",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$hLJx12Lqjc1kJuyFL1YSwOyjFix7u6oMg5LKgmYaXQXHJ6aLRwu16");

            migrationBuilder.CreateIndex(
                name: "IX_Advertisements_AdSlotId",
                table: "Advertisements",
                column: "AdSlotId");

            migrationBuilder.AddForeignKey(
                name: "FK_Advertisements_AdSlots_AdSlotId",
                table: "Advertisements",
                column: "AdSlotId",
                principalTable: "AdSlots",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Advertisements_AdSlots_AdSlotId",
                table: "Advertisements");

            migrationBuilder.DropIndex(
                name: "IX_Advertisements_AdSlotId",
                table: "Advertisements");

            migrationBuilder.DropColumn(
                name: "AdSlotId",
                table: "Advertisements");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "Advertisements");

            migrationBuilder.DropColumn(
                name: "BuyerPhone",
                table: "Advertisements");

            migrationBuilder.DropColumn(
                name: "Days",
                table: "Advertisements");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Advertisements");

            migrationBuilder.UpdateData(
                table: "Admins",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$mzCNEu6VpXfYM9Hlor4e0OzbrpfMiawzYRcmqAe9P6EmFkwofc5oi");
        }
    }
}
