using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WisdomITNews.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsletterFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "FullName", table: "NewsletterSubscribers", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Phone", table: "NewsletterSubscribers", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Source", table: "NewsletterSubscribers", type: "nvarchar(max)", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "FullName", table: "NewsletterSubscribers");
            migrationBuilder.DropColumn(name: "Phone", table: "NewsletterSubscribers");
            migrationBuilder.DropColumn(name: "Source", table: "NewsletterSubscribers");
        }
    }
}
