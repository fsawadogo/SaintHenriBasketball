using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaintHenriBasketball.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarFeedToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalendarFeedToken",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CalendarFeedToken",
                table: "Users",
                column: "CalendarFeedToken",
                unique: true,
                filter: "[CalendarFeedToken] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_CalendarFeedToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CalendarFeedToken",
                table: "Users");
        }
    }
}
