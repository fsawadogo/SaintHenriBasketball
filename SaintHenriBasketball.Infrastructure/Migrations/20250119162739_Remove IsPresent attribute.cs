using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaintHenriBasketball.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIsPresentattribute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPresent",
                table: "SessionAttendances");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPresent",
                table: "SessionAttendances",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
